using LIN.Runtime;
using LIN.Adapters;
using LIN.Helpers;
using LIN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Executors;

public sealed class LinOpenExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Func<LinAdapterType, ILinAdapter> _create;
    public LinOpenExecutor() : this(LinAdapterFactory.Create) { }
    internal LinOpenExecutor(Func<LinAdapterType, ILinAdapter> create) => _create = create;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = PluginCommon.ExecutionEpoch.Capture(context.Resources, "LIN");
            var step = context.CurrentStep!.Step;
            var serializer = new LinOpenPlugin().CreateSerializer();
            var setting = (LinOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs), "打开总超时必须大于 0");
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            int Remaining()
            {
                int left = setting.TimeoutMs - (int)Math.Min(int.MaxValue, deadline.ElapsedMilliseconds);
                return left > 0 ? left : throw new TimeoutException($"LIN 打开超过总期限 {setting.TimeoutMs} ms");
            }
            var channel = await Evaluator.EvalStringAsync(setting.Channel, context);
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);

            var key = LinHelper.GetAdapterKey(connName);

            using var registryLease = await LinRegistryGuard.Enter(context.Resources, cancellationToken, Remaining(), key);
            epoch.ThrowIfExpired();
            if (context.Resources.TryGet<ILinAdapter>(key, out var previous))
            {
                if (previous is not LinConnectionResource retiring || (!retiring.IsClosing && !retiring.IsClosed))
                    throw new InvalidOperationException($"LIN 连接 {connName} 已存在，请先确认关闭");
                try { if (!retiring.IsClosed) await retiring.ClosePhysicalAsync(Remaining(), cancellationToken); }
                catch (Exception ex) when (retiring.IsClosed && !cancellationToken.IsCancellationRequested)
                { context.Log(LogLevel.Error, "旧 LIN 连接已释放；记录历史收尾错误后继续本次打开", ex.ToString()); }
                finally
                {
                    if (retiring.IsClosed && context.Resources.TryGet<ILinAdapter>(key, out var current) && ReferenceEquals(current, retiring))
                        context.Resources.Remove(key);
                }
            }
            var physicalKey = LinConnectionResource.PhysicalKey(setting.AdapterType, channel);
            await PluginCommon.RetiringResources.WaitForPreviousAsync(physicalKey, Remaining(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            epoch.ThrowIfExpired();
            int openingBudget = Remaining();
            var adapter = new LinConnectionResource(_create(setting.AdapterType), physicalKey);
            PluginCommon.ExecutionScopedExecutor.Own(adapter.Dispose);
            try
            {
                epoch.ThrowIfExpired();
                if (!context.Resources.TryAdd(key, adapter, ResourceLifetime.Execution))
                    throw new InvalidOperationException($"LIN 连接 {connName} 已被其他步骤注册");
                epoch.ThrowIfExpired();
                await adapter.OpenAsync(new LinAdapterConfig
                {
                    Channel = channel,
                    BaudRate = setting.BaudRate,
                    LinVersion = setting.LinVersion,
                    IsMaster = setting.IsMaster,
                    RxQueueSize = setting.RxQueueSize
                }, openingBudget, cancellationToken, epoch.ThrowIfExpired);
                epoch.ThrowIfExpired();

            }
            catch (Exception openError)
            {
                // Freeze synchronously even when the caller's cancellation/deadline is exhausted.
                adapter.Dispose();
                // Caller cancellation ends only this wait. Dispose retains and tracks the
                // initializing driver until its real Open/Close calls have exited.
                if (openError is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    throw;
                try
                {
                    int cleanupBudget = Math.Max(1, setting.TimeoutMs - (int)Math.Min(int.MaxValue, deadline.ElapsedMilliseconds));
                    await adapter.CloseAsync(cleanupBudget);
                    if (context.Resources.TryGet<ILinAdapter>(key, out var current) && ReferenceEquals(current, adapter)) context.Resources.Remove(key);
                }
                catch (Exception cleanup)
                {
                    if (adapter.IsClosed && context.Resources.TryGet<ILinAdapter>(key, out var current) && ReferenceEquals(current, adapter)) context.Resources.Remove(key);
                    throw new AggregateException(adapter.IsClosed ? "LIN 打开失败，已释放句柄，但收尾报告错误" : "LIN 打开失败且收尾未确认，连接资源已保留，请执行关闭", openError, cleanup);
                }
                throw;
            }
            try
            {
                context.Log($"LIN 通道已打开: {channel} ({setting.AdapterType}, LIN {setting.LinVersion}, {setting.BaudRate} bps, {(setting.IsMaster ? "主节点" : "从节点")})");
                cancellationToken.ThrowIfCancellationRequested();
                epoch.ThrowIfExpired();
                _ = Remaining();
            }
            catch { adapter.Dispose(); throw; }

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
                    Error = ErrorInfo.FromException(ex, $"打开 LIN 通道失败: {ex.Message}")
                }
            };
        }
    }
}
