using System.Reflection;
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using PluginCommon;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace Modbus.ContractTests;

[Collection("Modbus followup isolated")]
public sealed class ExecutionScopeStopMechanismTests
{
    [Fact]
    public async Task Already_scheduled_resource_close_does_not_inherit_the_later_retired_step_context()
    {
        using var caller = new CancellationTokenSource();
        var registry = SdkRegistryForwarder.NewActual();
        var (context, state) = PublicationContextProxy.Create(new ModbusReadPlugin(), new ModbusReadSetting()); state.ResourcesOverride = registry;
        var gate = new PendingIoGate();
        var nativeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nativeExit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeScheduled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stepExit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? closing = null, io = null;
        int releases = 0;
        var running = ExecutionScopedExecutor.Create("Modbus", () => new Probe(async (_, _) =>
        {
            io = gate.RunAsync(async () => { nativeEntered.TrySetResult(); await nativeExit.Task; }, 30000, "真实工作", default);
            await nativeEntered.Task;
            closing = gate.CloseAsync(() => { ExecutionScopedExecutor.ThrowIfCurrentStepRetired(); releases++; }, 5000, default);
            closeScheduled.TrySetResult();
            await stepExit.Task; return Completed();
        })).ExecuteAsync(context, caller.Token);
        try
        {
            await closeScheduled.Task.WaitAsync(TimeSpan.FromSeconds(5)); caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await running.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            Assert.False(gate.IsClosed); Assert.Equal(0, releases);
            nativeExit.TrySetResult();
            await closing!.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(gate.IsClosed); Assert.Equal(1, releases);
        }
        finally
        {
            nativeExit.TrySetResult(); stepExit.TrySetResult();
            if (io != null) try { await io; } catch (InvalidOperationException) { }
            if (closing != null) try { await closing; } catch (InvalidOperationException) { }
            SdkRegistryForwarder.Cleanup(registry, true);
        }
    }

    [Fact]
    public async Task A_long_step_does_not_retain_thousands_of_finished_operation_registrations()
    {
        using var caller = new CancellationTokenSource();
        var registry = SdkRegistryForwarder.NewActual();
        var (context, state) = PublicationContextProxy.Create(new ModbusReadPlugin(), new ModbusReadSetting()); state.ResourcesOverride = registry;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int ownersStopped = 0, completedOperationsStopped = 0, registrations = -1;
        var running = ExecutionScopedExecutor.Create("Modbus", () => new Probe(async (_, _) =>
        {
            ExecutionScopedExecutor.Own(() => ownersStopped++);
            for (int i = 0; i < 2000; i++)
                using (ExecutionScopedExecutor.OwnOperation(() => completedOperationsStopped++)) { }
            registrations = ExecutionScopedExecutor.CurrentOwnerRegistrationCount;
            entered.TrySetResult(); await exit.Task; return Completed();
        })).ExecuteAsync(context, caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.Equal(1, registrations);
            caller.Cancel(); Assert.Equal(TestStatus.Aborted, (await running.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            Assert.Equal(1, ownersStopped); Assert.Equal(0, completedOperationsStopped);
        }
        finally { exit.TrySetResult(); SdkRegistryForwarder.Cleanup(registry, true); }
    }

    [Fact]
    public async Task Late_full_owner_constructor_after_caller_abort_retains_and_releases_both_staged_handles_once()
    {
        using var guard = new CancellationTokenSource(10000);
        using var caller = new CancellationTokenSource();
        var registry = SdkRegistryForwarder.NewActual();
        var (context, state) = PublicationContextProxy.Create(new ModbusReadPlugin(), new ModbusReadSetting());
        state.ResourcesOverride = registry;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var master = DispatchProxy.Create<IModbusMaster, ReadingMaster>();
        var probe = (ReadingMaster)(object)master;
        int released = 0, published = 0;
        var running = ExecutionScopedExecutor.Create("Modbus", () => new Probe(async (_, _) =>
        {
            entered.TrySetResult(); await release.Task;
            try { _ = new ModbusConnectionResource(master, new ReleaseHandle(() => Interlocked.Increment(ref released)), 1000); published++; return Completed(); }
            finally { finished.TrySetResult(); }
        })).ExecuteAsync(context, caller.Token);
        try
        {
            await entered.Task.WaitAsync(guard.Token); caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await running.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(0, released); Assert.Equal(0, probe.Disposals);
            release.TrySetResult(); await finished.Task.WaitAsync(guard.Token);
            while (Volatile.Read(ref probe.Disposals) != 1 || Volatile.Read(ref released) != 1) await Task.Delay(1, guard.Token);
            Assert.Equal(0, published); Assert.Equal(1, probe.Disposals); Assert.Equal(1, released);
        }
        finally { release.TrySetResult(); SdkRegistryForwarder.Cleanup(registry, true); }
    }

    [Fact]
    public async Task Aborted_is_published_only_after_new_owner_freezes_but_does_not_join_its_real_native_exit()
    {
        using var caller = new CancellationTokenSource();
        var registry = SdkRegistryForwarder.NewActual();
        var (context, state) = PublicationContextProxy.Create(new ModbusReadPlugin(), new ModbusReadSetting());
        state.ResourcesOverride = registry;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nativeExit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int stopped = 0;
        var execution = ExecutionScopedExecutor.Create("Modbus", () => new Probe(async (_, _) =>
        {
            ExecutionScopedExecutor.Own(() => Interlocked.Increment(ref stopped));
            entered.TrySetResult(); await nativeExit.Task; return Completed();
        })).ExecuteAsync(context, caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await execution.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            Assert.Equal(1, stopped); Assert.False(nativeExit.Task.IsCompleted);
        }
        finally { nativeExit.TrySetResult(); SdkRegistryForwarder.Cleanup(registry, true); }
    }

    [Fact]
    public async Task Successful_published_owner_does_not_inherit_the_old_step_tokens_lifetime()
    {
        using var caller = new CancellationTokenSource();
        var registry = SdkRegistryForwarder.NewActual();
        var (context, state) = PublicationContextProxy.Create(new ModbusReadPlugin(), new ModbusReadSetting());
        state.ResourcesOverride = registry;
        int stopped = 0;
        var executor = ExecutionScopedExecutor.Create("Modbus", () => new Probe((_, _) =>
        { ExecutionScopedExecutor.Own(() => Interlocked.Increment(ref stopped)); return Task.FromResult(Completed()); }));
        try
        {
            Assert.Equal(TestStatus.Completed, (await executor.ExecuteAsync(context, caller.Token)).StepResult.Status);
            caller.Cancel(); Assert.Equal(0, stopped);
            ExecutionEpoch.Capture(registry, "Modbus").ThrowIfExpired();
        }
        finally { SdkRegistryForwarder.Cleanup(registry, true); }
    }

    [Fact]
    public async Task Caller_can_abort_a_noncooperative_expression_before_sdk_cleanup_without_waiting_for_expression_exit()
    {
        using var caller = new CancellationTokenSource();
        var registry = SdkRegistryForwarder.NewActual();
        var (context, state) = PublicationContextProxy.Create(new ModbusReadPlugin(), new ModbusReadSetting());
        state.ResourcesOverride = registry;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int submissions = 0;
        var executor = ExecutionScopedExecutor.Create("Modbus", () => new Probe(async (ctx, _) =>
        {
            entered.TrySetResult();
            try { await release.Task; ExecutionEpoch.Capture(ctx.Resources, "Modbus").ThrowIfExpired(); submissions++; return Completed(); }
            finally { exited.TrySetResult(); }
        }));
        var running = executor.ExecuteAsync(context, caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await running.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            Assert.False(exited.Task.IsCompleted);
            release.TrySetResult(); await exited.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, submissions);
            ExecutionEpoch.Capture(registry, "Modbus").ThrowIfExpired(); // Other steps' execution scope was not retired.
        }
        finally { release.TrySetResult(); await exited.Task.WaitAsync(TimeSpan.FromSeconds(5)); SdkRegistryForwarder.Cleanup(registry, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_inner_Error_is_not_overwritten_by_concurrent_caller_cancellation(bool freezeFails)
    {
        using var caller = new CancellationTokenSource();
        var registry = SdkRegistryForwarder.NewActual();
        var (context, state) = PublicationContextProxy.Create(new ModbusReadPlugin(), new ModbusReadSetting());
        state.ResourcesOverride = registry;
        var expected = new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error,
            Error = new ErrorInfo { Message = "设备明确拒绝了请求", Detail = "必须保留的原始错误详情" } } };
        var executor = ExecutionScopedExecutor.Create("Modbus", () => new Probe((_, _) =>
        {
            if (freezeFails) ExecutionScopedExecutor.Own(() => throw new InvalidOperationException("fake request stop failed"));
            caller.Cancel(); return Task.FromResult(expected);
        }));
        try
        {
            Assert.Same(expected, await executor.ExecuteAsync(context, caller.Token));
            Assert.Contains("必须保留的原始错误详情", expected.StepResult.Error!.Detail);
            if (freezeFails) Assert.Contains("fake request stop failed", expected.StepResult.Error.Detail);
        }
        finally { SdkRegistryForwarder.Cleanup(registry, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Open_failure_compensation_does_not_undo_the_sdk_stop_notification(bool all)
    {
        using var guard = new CancellationTokenSource(10000);
        var registry = SdkRegistryForwarder.NewActual();
        var (context, state) = PublicationContextProxy.Create(new ModbusConnectPlugin(), new ModbusConnectSetting());
        state.ResourcesOverride = registry;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int releases = 0;
        var owner = new ModbusConnectionResource(30000);
        var opening = ModbusLifecycle.OpenRegisteredAsync(context, "stop-open", owner, async (resource, _) =>
        {
            resource.OwnTransport(new ReleaseHandle(() => Interlocked.Increment(ref releases)));
            resource.OwnMaster(DispatchProxy.Create<IModbusMaster, ReadingMaster>());
            entered.TrySetResult(); await release.Task;
        }, default);
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            SdkRegistryForwarder.Cleanup(registry, all);
            await Assert.ThrowsAsync<InvalidOperationException>(() => opening.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(owner.IsClosed); Assert.Equal(0, releases);
            release.TrySetResult();
            await owner.CloseAsync(2000, guard.Token);
            Assert.True(owner.IsClosed); Assert.Equal(1, releases);
        }
        finally
        {
            release.TrySetResult();
            try { await opening.WaitAsync(guard.Token); } catch (InvalidOperationException) { }
            await owner.CloseAsync(2000, guard.Token); SdkRegistryForwarder.Cleanup(registry, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Detached_old_executor_cannot_capture_the_next_epoch_from_a_changed_context(bool newDebugRegistry)
    {
        var registry = SdkRegistryForwarder.NewActual();
        var plugin = new ModbusReadPlugin();
        var (context, state) = PublicationContextProxy.Create(plugin, new ModbusReadSetting());
        state.ResourcesOverride = registry;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int lateSubmissions = 0;
        var executor = ExecutionScopedExecutor.Create("Modbus", () => new Probe(async (ctx, _) =>
        {
            ExecutionEpoch.Capture(ctx.Resources, "Modbus").ThrowIfExpired();
            entered.TrySetResult();
            try
            {
                await release.Task;
                ExecutionEpoch.Capture(ctx.Resources, "Modbus").ThrowIfExpired();
                Interlocked.Increment(ref lateSubmissions);
                return Completed();
            }
            finally { finished.TrySetResult(); }
        }));
        var old = executor.ExecuteAsync(context);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            SdkRegistryForwarder.Cleanup(registry, newDebugRegistry);
            Assert.Equal(TestStatus.Error, (await old.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            if (newDebugRegistry) state.ResourcesOverride = SdkRegistryForwarder.NewActual();
            var next = ExecutionScopedExecutor.Create("Modbus", () => new Probe((ctx, _) =>
            {
                ExecutionEpoch.Capture(ctx.Resources, "Modbus").ThrowIfExpired();
                return Task.FromResult(Completed());
            }));
            Assert.Equal(TestStatus.Completed, (await next.ExecuteAsync(context)).StepResult.Status);
            Assert.False(finished.Task.IsCompleted);
            release.TrySetResult();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, lateSubmissions);
            ExecutionEpoch.Capture(state.ResourcesOverride!, "Modbus").ThrowIfExpired();
        }
        finally { release.TrySetResult(); await finished.Task.WaitAsync(TimeSpan.FromSeconds(5)); SdkRegistryForwarder.Cleanup(state.ResourcesOverride!, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sdk_cleanup_retires_a_step_in_a_long_batch_interval_before_its_original_delay_expires(bool all)
    {
        using var caller = new CancellationTokenSource();
        var registry = SdkRegistryForwarder.NewActual();
        var plugin = new ModbusBatchReadPlugin();
        var setting = new ModbusBatchReadSetting { ConnectionName = "\"C\"", IntervalMs = 30000,
            Items = [new ModbusBatchItem { ResultVariable = "Locals.first" }, new ModbusBatchItem { ResultVariable = "Locals.second" }] };
        var (context, state) = PublicationContextProxy.Create(plugin, setting);
        state.ResourcesOverride = registry;
        state.Variables = new() { ["Locals.first"] = (ushort)0, ["Locals.second"] = (ushort)0 };
        var master = DispatchProxy.Create<IModbusMaster, ReadingMaster>();
        var probe = (ReadingMaster)(object)master;
        var owner = new ModbusConnectionResource(master, new Handle(), 1000);
        registry.Set(ModbusHelper.GetConnectionKey("C"), owner, ResourceLifetime.Execution);
        var running = plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
        try
        {
            // Publication is the deterministic boundary immediately preceding the long interval.
            await probe.Read.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var publicationDeadline = new CancellationTokenSource(10000);
            while (!state.Published.Any(p => p.Path == "Locals.first")) await Task.Delay(1, publicationDeadline.Token);
            SdkRegistryForwarder.Cleanup(registry, all);
            var result = await running.WaitAsync(TimeSpan.FromMilliseconds(1000));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.False(caller.IsCancellationRequested);
            Assert.NotEmpty(result.StepResult.Error!.Detail!);
            Assert.Equal(1, probe.Reads);
            Assert.DoesNotContain(state.Published, p => p.Path == "Locals.second");
        }
        finally { caller.Cancel(); await running.WaitAsync(TimeSpan.FromSeconds(5)); await owner.CloseAsync(2000, default); SdkRegistryForwarder.Cleanup(registry, true); }
    }

    public class ReadingMaster : DispatchProxy
    {
        internal int Reads;
        internal int Disposals;
        internal readonly TaskCompletionSource Read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "ReadHoldingRegistersAsync") { Interlocked.Increment(ref Reads); Read.TrySetResult(); return Task.FromResult(new ushort[] { 7 }); }
            if (method.Name == "Dispose") { Interlocked.Increment(ref Disposals); return null; }
            return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
    private sealed class Handle : IDisposable { public void Dispose() { } }
    private sealed class ReleaseHandle(Action release) : IDisposable { public void Dispose() => release(); }
    private static ExecutionResult Completed() => new() { StepResult = new StepResult { Status = TestStatus.Completed } };
    private sealed class Probe(Func<IExecutionContext, CancellationToken, Task<ExecutionResult>> execute) : IStepExecutor
    {
        public Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default) => execute(context, cancellationToken);
    }
}
