using System.Reflection;
using System.Runtime.ExceptionServices;
using Modbus.Helpers;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace Modbus.ContractTests;

[CollectionDefinition("Modbus followup isolated", DisableParallelization = true)]
public sealed class ModbusFollowupCollection { }

[Collection("Modbus followup isolated")]
public sealed class LifecycleFollowupTests
{
    [Theory]
    [InlineData("read", "cancel")] [InlineData("write", "cancel")]
    [InlineData("batchRead", "cancel")] [InlineData("batchWrite", "cancel")]
    [InlineData("read", "CleanupExecution")] [InlineData("write", "CleanupExecution")]
    [InlineData("batchRead", "CleanupExecution")] [InlineData("batchWrite", "CleanupExecution")]
    [InlineData("read", "CleanupAll")] [InlineData("write", "CleanupAll")]
    [InlineData("batchRead", "CleanupAll")] [InlineData("batchWrite", "CleanupAll")]
    [InlineData("read", "missing")] [InlineData("write", "missing")]
    [InlineData("batchRead", "missing")] [InlineData("batchWrite", "missing")]
    public async Task Failed_lookup_keeps_original_cancellation_and_scope_diagnostics(string kind, string boundary)
    {
        IStepPlugin plugin = kind switch
        {
            "read" => new ModbusReadPlugin(), "write" => new ModbusWritePlugin(),
            "batchRead" => new ModbusBatchReadPlugin(), _ => new ModbusBatchWritePlugin()
        };
        var actual = NewRegistry();
        var resources = DispatchProxy.Create<IResourceRegistry, FollowupRegistry>();
        var probe = (FollowupRegistry)(object)resources; probe.Inner = actual;
        var (context, state) = PublicationContextProxy.Create(plugin, plugin.CreateSerializer().CreateDefault());
        state.ResourcesOverride = resources;
        using var caller = new CancellationTokenSource();
        probe.BeforeLookup = () =>
        {
            if (boundary == "cancel") caller.Cancel();
            else if (boundary != "missing") Cleanup(actual, boundary);
        };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.True(probe.Entered);
            Assert.Equal(boundary == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            if (boundary.StartsWith("Cleanup", StringComparison.Ordinal))
                Assert.Contains("作用域", result.StepResult.Error!.Message + result.StepResult.Error.Detail);
            Assert.Empty(state.Published);
            var next = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, next.StepResult.Status);
            Assert.Contains("未找到 Modbus 连接", next.StepResult.Error!.Message);
        }
        finally { Cleanup(actual, "CleanupAll"); }
    }

    [Theory]
    [InlineData("callback")]
    [InlineData("interrupt")]
    [InlineData("report")]
    public async Task Dispose_never_waits_on_callbacks_native_interrupt_or_diagnostic_logging(string stage)
    {
        using var guard = new CancellationTokenSource(15000);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Hold()
        {
            entered.TrySetResult();
            if (!release.Wait(10000)) throw new TimeoutException("test cleanup barrier");
        }
        var transport = new ControlledTransport { Interrupt = stage switch
        {
            "interrupt" => Hold,
            "report" => () => throw new InvalidOperationException("fake interrupt failure"),
            _ => () => { }
        } };
        var owner = NewOwner(transport, _ => { if (stage == "report") Hold(); });
        var stop = Lifetime(owner);
        using var registration = stage == "callback" ? stop.Token.Register(Hold) : default;
        var disposing = Task.Run(owner.Dispose);
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            Assert.Same(disposing, await Task.WhenAny(disposing, Task.Delay(300, guard.Token)));
            await disposing;
            if (stage != "report") Assert.False(owner.IsClosed); // Pending callbacks/interrupt still own the handle.
        }
        finally
        {
            release.Set(); await disposing.WaitAsync(guard.Token);
            try { await owner.CloseAsync(2000, guard.Token); } catch (Exception) when (owner.IsClosed) { }
        }
        Assert.True(owner.IsClosed); Assert.Equal(1, transport.Releases);
    }

    [Fact]
    public async Task Throwing_cancellation_callback_is_reported_without_throwing_from_Dispose()
    {
        using var guard = new CancellationTokenSource(10000);
        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new ControlledTransport();
        var owner = NewOwner(transport, error => reported.TrySetResult(error));
        using var registration = Lifetime(owner).Token.Register(() => throw new InvalidOperationException("fake cancellation callback failure"));
        Assert.Null(Record.Exception(owner.Dispose));
        var diagnostic = await reported.Task.WaitAsync(guard.Token);
        Assert.Contains("fake cancellation callback failure", diagnostic.ToString());
        try { await owner.CloseAsync(2000, guard.Token); } catch (Exception) when (owner.IsClosed) { }
        Assert.True(owner.IsClosed); Assert.Equal(1, transport.Releases);
    }

    [Fact]
    public async Task Explicit_close_returns_its_configured_timeout_while_a_cancellation_callback_is_still_running()
    {
        using var guard = new CancellationTokenSource(15000);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = NewOwner(new ControlledTransport());
        using var registration = Lifetime(owner).Token.Register(() =>
        { entered.TrySetResult(); if (!release.Wait(10000)) throw new TimeoutException("test cancellation barrier"); });
        var closing = Task.Run(() => owner.CloseAsync(100, default));
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            Assert.Same(closing, await Task.WhenAny(closing, Task.Delay(400, guard.Token)));
            var timeout = await Assert.ThrowsAsync<TimeoutException>(() => closing);
            Assert.Contains("100", timeout.Message); Assert.False(owner.IsClosed);
        }
        finally { release.Set(); try { await closing; } catch (TimeoutException) { } await owner.CloseAsync(2000, guard.Token); }
        Assert.True(owner.IsClosed);
    }

    [Fact]
    public async Task A_failed_native_interrupt_can_be_retried_without_releasing_a_still_running_transaction()
    {
        using var guard = new CancellationTokenSource(10000);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int interrupts = 0;
        var transport = new ControlledTransport { Interrupt = () =>
        {
            if (Interlocked.Increment(ref interrupts) == 1) throw new InvalidOperationException("fake first interrupt failed");
            complete.TrySetResult();
        } };
        var owner = NewOwner(transport);
        var io = owner.WithTimeoutAsync(async () => { entered.TrySetResult(); await complete.Task; }, 5000, "fake pending transaction", default);
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            var failed = await Record.ExceptionAsync(() => owner.CloseAsync(100, default));
            Assert.NotNull(failed); Assert.Contains("fake first interrupt failed", failed.ToString());
            Assert.False(owner.IsClosed); Assert.Equal(0, transport.Releases);
            await owner.CloseAsync(2000, default).WaitAsync(guard.Token);
            Assert.Equal(2, interrupts); Assert.True(owner.IsClosed); Assert.Equal(1, transport.Releases);
        }
        finally
        {
            complete.TrySetResult();
            try { await io; } catch (InvalidOperationException) { }
            try { await owner.CloseAsync(2000, default); } catch (Exception) when (owner.IsClosed) { }
        }
    }

    private static ModbusConnectionResource NewOwner(ControlledTransport transport, Action<Exception>? report = null)
        => new(DispatchProxy.Create<IModbusMaster, EmptyMaster>(), transport, 1000, report);
    private static CancellationTokenSource Lifetime(ModbusConnectionResource owner)
        => (CancellationTokenSource)typeof(ModbusConnectionResource).GetField("_lifetime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static IResourceRegistry NewRegistry() => (IResourceRegistry)Activator.CreateInstance(
        typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    private static void Cleanup(IResourceRegistry registry, string name)
        => registry.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);

    private sealed class ControlledTransport : IDisposable, ICancellableModbusTransport
    {
        internal Action Interrupt = () => { };
        internal int Releases;
        public void CancelPendingIo() => Interrupt();
        public void Dispose() => Interlocked.Increment(ref Releases);
    }
    public class EmptyMaster : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method!.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
    }
    public class FollowupRegistry : DispatchProxy
    {
        internal IResourceRegistry Inner = null!;
        internal Action? BeforeLookup;
        internal bool Entered;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "TryGet" && method.GetGenericArguments()[0] == typeof(ModbusConnectionResource))
            { Entered = true; Interlocked.Exchange(ref BeforeLookup, null)?.Invoke(); }
            try { return method.Invoke(Inner, args); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        }
    }
}
