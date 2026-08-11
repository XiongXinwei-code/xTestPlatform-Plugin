using Ivi.Visa;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.Executors;

/// <summary>
/// VISA 关闭会话执行器，释放仪器连接资源
/// </summary>
public sealed class VisaCloseExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>执行 VISA 关闭会话操作</summary>
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new VisaClosePlugin().CreateSerializer();
        var setting = (VisaCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = VisaHelper.GetSessionKey(connName);

            if (context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) && obj is IMessageBasedSession session)
            {
                session.Dispose();
                context.CurrentStep.RuntimeData.Remove(key);
            }
            context.CurrentStep.RuntimeData.Remove(VisaHelper.GetTerminatorKey(connName));

            context.LogAction?.Invoke($"VISA 会话已关闭: {connName}");
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已关闭: {connName}" }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"VISA 关闭失败: {ex.Message}" }
                }
            };
        }
    }
}
