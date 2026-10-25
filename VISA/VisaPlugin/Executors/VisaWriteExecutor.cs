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
/// VISA 写入执行器，向仪器发送 SCPI 命令
/// </summary>
public sealed class VisaWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Func<string, IExecutionContext, Task<string>> _evaluate;
    public VisaWriteExecutor() : this((expression, context) => Evaluator.EvalStringAsync(expression, context)) { }
    internal VisaWriteExecutor(Func<string, IExecutionContext, Task<string>> evaluate) => _evaluate = evaluate;

    /// <summary>执行 VISA 写入操作</summary>
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new VisaWritePlugin().CreateSerializer();
            var setting = (VisaWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var connName = await _evaluate(setting.ConnectionName, context);
            epoch.ThrowIfExpired();
            var command = await _evaluate(setting.Command, context);
            epoch.ThrowIfExpired();
            if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("SCPI 命令求值结果不能为空");
            var key = VisaHelper.GetSessionKey(connName);

            if (!context.Resources.TryGet<VisaSessionResource>(key, out var resource))
            {
                epoch.ThrowIfExpired();
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
            await resource.RunBlockingAsync(session => VisaHelper.Write(session, command, resource.Terminator, () => { epoch.ThrowIfExpired(); resource.ThrowIfSubmissionStopped(); }),
                resource.IoTimeoutMs, "VISA 写入", cancellationToken);

            context.Log($"VISA Write: {command}");
            epoch.ThrowIfExpired();
            VisaResultPublisher.EnsureActive(context, key, resource, cancellationToken);
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = command }
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
                    Error = ErrorInfo.FromException(ex, $"VISA 写入超时: {ex.Message}")
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
                    Error = ErrorInfo.FromException(ex, $"VISA 写入失败: {ex.Message}")
                }
            };
        }
    }

    /// <summary>获取打开会话时保存的终止符，未找到时默认换行符</summary>
    private static string GetTerminator(IExecutionContext context, string connName) =>
        context.Resources.TryGet<string>(VisaHelper.GetTerminatorKey(connName), out var term) ? term : "\n";
}
