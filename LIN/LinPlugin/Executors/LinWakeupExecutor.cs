using LIN.Adapters;
using LIN.Helpers;
using LIN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Executors;

public sealed class LinWakeupExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new LinWakeupPlugin().CreateSerializer();
        var setting = (LinWakeupSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = LinHelper.GetAdapterKey(connName);

            if (!context.Resources.TryGet<ILinAdapter>(key, out var adapter))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error  = new ErrorInfo { Message = $"LIN 通道未找到: {connName}，请先执行 LIN_Open 步骤" }
                    }
                };
            }

            cancellationToken.ThrowIfCancellationRequested();
            adapter.Wakeup(setting.WakeupMode == LinWakeupMode.Remote);
            context.LogAction?.Invoke($"LIN 总线已唤醒: {connName} ({(setting.WakeupMode == LinWakeupMode.Remote ? "总线唤醒" : "本地唤醒")})");

            if (setting.PostWakeupDelayMs > 0)
            {
                await Task.Delay(setting.PostWakeupDelayMs, cancellationToken);
                context.LogAction?.Invoke($"唤醒后延时 {setting.PostWakeupDelayMs}ms 完成，从节点应已就绪");
            }

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed }
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Aborted }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error  = ErrorInfo.FromException(ex, $"LIN 唤醒失败: {ex.Message}")
                }
            };
        }
    }
}
