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
        var step = context.CurrentStep!.Step;
        var serializer = new VisaQueryPlugin().CreateSerializer();
        var setting = (VisaQuerySetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var command = await Evaluator.EvalStringAsync(setting.Command, context);
            var varName = setting.ResultVariable;
            var key = VisaHelper.GetSessionKey(connName);

            if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not IMessageBasedSession session)
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

            var response = VisaHelper.Query(session, command, setting.TrimResponse, GetTerminator(context, connName));
            context.SetVariable(varName, response);

            context.LogAction?.Invoke($"VISA Query: {command} => {response}");
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = response }
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"VISA 查询失败: {ex.Message}" }
                }
            };
        }
    }

    /// <summary>获取打开会话时保存的终止符，未找到时默认换行符</summary>
    private static string GetTerminator(IExecutionContext context, string connName) =>
        context.CurrentStep!.RuntimeData.TryGetValue(VisaHelper.GetTerminatorKey(connName), out var t) && t is string term ? term : "\n";
}
