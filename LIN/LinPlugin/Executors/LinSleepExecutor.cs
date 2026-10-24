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
        try
        {
            var guard = new LIN.Runtime.LinExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new LinSleepPlugin().CreateSerializer();
            var setting = (LinSleepSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (!Enum.IsDefined(setting.SleepMode)) throw new ArgumentException("LIN 睡眠模式无效");
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = LinHelper.GetAdapterKey(connName);

            if (!context.Resources.TryGet<ILinAdapter>(key, out var adapter))
            {
                guard.Check();
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error  = new ErrorInfo { Message = $"LIN 通道未找到: {connName}，请先执行 LIN_Open 步骤" }
                    }
                };
            }
            guard.Watch(key, adapter);

            cancellationToken.ThrowIfCancellationRequested();
            await LIN.Runtime.LinConnectionResource.For(adapter).SleepAsync(setting.SleepMode == LinSleepMode.Remote, setting.TimeoutMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            guard.Log($"LIN 总线已进入睡眠: {connName} ({(setting.SleepMode == LinSleepMode.Remote ? "总线睡眠" : "本地睡眠")})");

            if (setting.PostSleepDelayMs > 0)
            {
                await Task.Delay(setting.PostSleepDelayMs, cancellationToken);
                guard.Log($"入睡后延时 {setting.PostSleepDelayMs}ms 完成，从节点应已入睡");
            }

            guard.Check();

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
                    Error  = ErrorInfo.FromException(ex, $"LIN 睡眠失败: {ex.Message}")
                }
            };
        }
    }
}
