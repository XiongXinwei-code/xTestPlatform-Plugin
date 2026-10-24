using PluginCommon;
using Ivi.Visa;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.Executors;

/// <summary>
/// VISA 查询执行器，发送 SCPI 命令并读取响应
/// </summary>
public sealed class VisaQueryExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>执行 VISA 查询操作</summary>
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new VisaQueryPlugin().CreateSerializer();
            var setting = (VisaQuerySetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            epoch.ThrowIfExpired();
            var command = await Evaluator.EvalStringAsync(setting.Command, context);
            epoch.ThrowIfExpired();
            if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("SCPI 查询命令求值结果不能为空");
            var varName = setting.ResultVariable;
            var key = VisaHelper.GetSessionKey(connName);

            if (!context.Resources.TryGet<VisaSessionResource>(key, out var resource))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"未找到 VISA 会话: {connName}" }
                    }
                };
            }

            epoch.ThrowIfExpired();
            var response = await resource.RunBlockingAsync(
                session => VisaHelper.Query(session, command, setting.TrimResponse, resource.Terminator, () => { epoch.ThrowIfExpired(); resource.ThrowIfSubmissionStopped(); }),
                resource.IoTimeoutMs, "VISA 查询", cancellationToken);
            epoch.ThrowIfExpired();
            VisaResultPublisher.EnsureActive(context, key, resource, cancellationToken);
            context.SetVariable(varName, response);

            context.Log($"VISA Query: {command} => {response}");
            epoch.ThrowIfExpired();
            VisaResultPublisher.EnsureActive(context, key, resource, cancellationToken);
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = response }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (TimeoutException ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"VISA 查询超时: {ex.Message}")
                }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"VISA 查询失败: {ex.Message}")
                }
            };
        }
    }

    /// <summary>获取打开会话时保存的终止符，未找到时默认换行符</summary>
    private static string GetTerminator(IExecutionContext context, string connName) =>
        context.Resources.TryGet<string>(VisaHelper.GetTerminatorKey(connName), out var term) ? term : "\n";
}
