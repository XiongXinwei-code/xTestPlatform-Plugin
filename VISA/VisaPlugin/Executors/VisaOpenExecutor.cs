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
            // 若已存在同名会话（序列异常终止未关闭），先销毁旧会话
            if (VisaHelper.TryRemoveSession(connName, out var oldSession) && oldSession is not null)
            {
                try { oldSession.Dispose(); } catch { /* 忽略销毁异常 */ }
                context.LogAction?.Invoke($"VISA 会话 {connName} 检测到已有连接，已自动销毁旧会话");
            }

            var session = VisaHelper.OpenSession(resource, setting.OpenTimeoutMs, setting.IoTimeoutMs, setting.Terminator);

            VisaHelper.StoreSession(connName, session);

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
