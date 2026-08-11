using Ivi.Visa;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.Executors;

/// <summary>
/// VISA 打开会话执行器，建立与仪器的 VISA 连接
/// </summary>
public sealed class VisaOpenExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>执行 VISA 打开会话操作</summary>
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new VisaOpenPlugin().CreateSerializer();
        var setting = (VisaOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var resource = await Evaluator.EvalStringAsync(setting.ResourceString, context);
            var key = VisaHelper.GetSessionKey(connName);

            var session = VisaHelper.OpenSession(resource, setting.OpenTimeoutMs, setting.IoTimeoutMs, setting.Terminator);

            // Set 会自动销毁同名旧会话（如上次运行异常终止未关闭的连接）
            context.Resources.Set(key, session);
            context.Resources.Set(VisaHelper.GetTerminatorKey(connName), VisaHelper.NormalizeTerminator(setting.Terminator));

            context.LogAction?.Invoke($"VISA 会话已打开: {connName} ({resource})");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已连接: {resource}" }
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
                    Error = new ErrorInfo { Message = $"VISA 打开失败: {ex.Message}" }
                }
            };
        }
    }
}
