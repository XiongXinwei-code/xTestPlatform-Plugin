using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanCyclicSendStartExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var setting = (CanCyclicSendStartSetting)new CanCyclicSendStartPlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            if (!context.Resources.TryGet<CanConnectionResource>(CanHelper.GetAdapterKey(connName), out var connection))
                throw new InvalidOperationException($"CAN 连接 {connName} 未找到，请使用新版 CAN_Open");
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            guard.Watch(CanHelper.GetAdapterKey(connName), connection);
            var messages = setting.Messages.Where(m => m.Enabled).ToArray();
            if (messages.Any(m => m.CycleTimeMs <= 0)) throw new ArgumentException("周期必须大于零");
            cancellationToken.ThrowIfCancellationRequested();
            if (messages.Length == 0) return new() { StepResult = new() { Status = TestStatus.Completed, Value = "没有启用的报文" } };
            var job = connection.StartJob(taskName, async token =>
            {
                // 保留逐报文独立调度；跟踪全部任务，取消后等待实际退出。
                using var groupStop = CancellationTokenSource.CreateLinkedTokenSource(token);
                var workers = messages.Select(message => Task.Run(async () =>
                {
                    try
                    {
                        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(message.CycleTimeMs));
                        while (await timer.WaitForNextTickAsync(groupStop.Token).ConfigureAwait(false))
                        {
                            var id = CanHelper.ParseCanId(await Evaluator.EvalStringAsync(message.CanId, context));
                            var data = CanHelper.ParseHexData(await Evaluator.EvalStringAsync(message.Data, context));
                            groupStop.Token.ThrowIfCancellationRequested();
                            guard.CheckLifetime();
                            connection.Write(new CanMessage { Id = id, Data = data, FrameType = message.FrameType });
                        }
                    }
                    catch { groupStop.Cancel(); throw; }
                })).ToArray();
                await Task.WhenAll(workers).ConfigureAwait(false);
            });
            try { guard.Check(); }
            catch { job.Dispose(); throw; } // 只冻结本次任务；连接所有者继续跟踪真实退出。
            return new() { StepResult = new() { Status = TestStatus.Completed, Value = $"周期任务已启动：{connName}/{taskName}；重复启动须先停止" } };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return new() { StepResult = new() { Status = TestStatus.Aborted } }; }
        catch (Exception ex)
        { return new() { StepResult = new() { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex, $"周期启动失败：{ex.Message}") } }; }
    }
}
