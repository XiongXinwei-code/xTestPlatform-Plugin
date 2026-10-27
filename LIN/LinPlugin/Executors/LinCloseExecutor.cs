using LIN.Runtime;
using LIN.Adapters;
using LIN.Helpers;
using LIN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Executors;

public sealed class LinCloseExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new LIN.Runtime.LinExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new LinClosePlugin().CreateSerializer();
            var setting = (LinCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs), "关闭总超时必须大于 0");
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            int Remaining()
            {
                int left = setting.TimeoutMs - (int)Math.Min(int.MaxValue, deadline.ElapsedMilliseconds);
                return left > 0 ? left : throw new TimeoutException($"LIN 关闭超过总期限 {setting.TimeoutMs} ms");
            }
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = LinHelper.GetAdapterKey(connName);
            guard.Check();

            context.Resources.TryGet<ILinAdapter>(key, out var expected);
            guard.Check();
            // Freeze the captured generation before waiting for a concurrent opening/registry lease.
            // Dispose only requests asynchronous cleanup; it does not pretend native I/O has exited.
            if (expected is LinConnectionResource captured) captured.Dispose();
            using var registryLease = await LinRegistryGuard.Enter(context.Resources, cancellationToken, Remaining(), key);
            guard.Check();
            if (context.Resources.TryGet<ILinAdapter>(key, out var adapter) && ReferenceEquals(adapter, expected))
            {
                var resource = LinConnectionResource.For(adapter);
                Exception? closeError = null;
                try { await resource.ClosePhysicalAsync(Remaining(), cancellationToken); }
                catch (Exception ex) { closeError = ex; }
                if (resource.IsClosed)
                {
                    if (context.Resources.TryGet<ILinAdapter>(key, out var current) && ReferenceEquals(current, adapter)) context.Resources.Remove(key);
                    // Never hold both a connection-name lease and a task-name
                    // lease, or hold either one while joining a managed worker.
                    registryLease.Dispose();
                    try
                    {
                        foreach (var job in resource.Jobs)
                            if (job.RegistrationKey is { } taskKey)
                            {
                                using var taskLease = await LinRegistryGuard.Enter(context.Resources, cancellationToken, Remaining(), taskKey);
                                guard.Check();
                                if (context.Resources.TryGet<LinCyclicJob>(taskKey, out var registered) && ReferenceEquals(job, registered))
                                    context.Resources.Remove(taskKey);
                            }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception ex) { closeError = closeError == null ? ex : new AggregateException("LIN 物理关闭与周期登记收尾同时报告错误", closeError, ex); }
                }
                if (closeError != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(closeError).Throw();
                await resource.WaitForWorkersAsync(Math.Max(0, setting.TimeoutMs - (int)Math.Min(int.MaxValue, deadline.ElapsedMilliseconds)), cancellationToken);
                if (resource.CloseWarning is { } warning)
                    context.Log(LogLevel.Warn, $"LIN 通道 {connName} 已关闭；保留历史通信或已退出回调的诊断", warning.ToString());
                guard.Log($"LIN 通道已关闭: {connName}");
            }
            else
            {
                guard.Log(LogLevel.Warn, $"LIN 通道未找到: {connName}");
            }

            guard.Check();

            _ = Remaining();
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
                    Error = ErrorInfo.FromException(ex, $"关闭 LIN 通道失败: {ex.Message}")
                }
            };
        }
    }
}
