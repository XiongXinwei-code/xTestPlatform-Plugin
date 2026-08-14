using LIN.Adapters;
using LIN.Helpers;
using LIN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Executors;

public sealed class LinSleepExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new LinSleepPlugin().CreateSerializer();
        var setting = (LinSleepSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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
            adapter.Sleep(setting.SleepMode == LinSleepMode.Remote);
            context.LogAction?.Invoke($"LIN 总线已进入睡眠: {connName} ({(setting.SleepMode == LinSleepMode.Remote ? "总线睡眠" : "本地睡眠")})");

            if (setting.PostSleepDelayMs > 0)
            {
                await Task.Delay(setting.PostSleepDelayMs, cancellationToken);
                context.LogAction?.Invoke($"入睡后延时 {setting.PostSleepDelayMs}ms 完成，从节点应已入睡");
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
                    Error  = new ErrorInfo { Message = $"LIN 睡眠失败: {ex.Message}" }
                }
            };
        }
    }
}
