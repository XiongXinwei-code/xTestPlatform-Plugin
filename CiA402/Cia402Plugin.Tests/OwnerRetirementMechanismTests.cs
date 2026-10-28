using CiA402.Core;
using CiA402.Models;
using CiA402.Transport;
using Xunit;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CiA402.Tests;

public sealed class OwnerRetirementMechanismTests
{
    [Fact]
    public async Task Close_created_by_original_catch_keeps_cleanup_independent_when_step_retires_later()
    {
        var entered = Signal(); var releaseAction = Signal(); var closeCreated = Signal(); var releaseExecutor = Signal(); var cleanupEntered = Signal();
        var raw = new ProbeAxisTransport();
        raw.WriteHandler = (index, data, ct) => { cleanupEntered.TrySetResult(); return raw.Drive.WriteObjectAsync(index, 0, data, ct); };
        var config = new BindAxisSetting { ShutdownTimeoutMs = 3000 };
        var session = new AxisSession(new AxisController(raw, config), config, default, (_, _) => { });
        var (context, _) = TestContext.Create(new ReadStatePlugin(), new ReadStateSetting());
        var create = typeof(AxisSession).Assembly.GetType("PluginCommon.ExecutionScopedExecutor", true)!
            .GetMethod("Create", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        using var actionCaller = new CancellationTokenSource();
        var factory = new Func<IStepExecutor>(() => new CallbackExecutor(async _ =>
        {
            try { await session.ExecuteAsync(async (_, _) => { entered.TrySetResult(); await releaseAction.Task; }, true, 20, actionCaller.Token); }
            catch (OperationCanceledException) { }
            closeCreated.TrySetResult(); // ExecuteAsync's original catch already called Dispose.
            await releaseExecutor.Task;
            return new() { StepResult = new() { Status = TestStatus.Completed } };
        }));
        var wrapper = (IStepExecutor)create.Invoke(null, ["CiA402", factory])!;
        using var stepCaller = new CancellationTokenSource();
        var execution = wrapper.ExecuteAsync(context, stepCaller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); actionCaller.Cancel();
            await closeCreated.Task.WaitAsync(TimeSpan.FromSeconds(3)); stepCaller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await execution.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            releaseAction.TrySetResult();
            try { await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(1)); }
            catch (TimeoutException ex) { throw new InvalidOperationException($"独立清理未进入底层；{session.DiagnosticContext}", ex); }
            await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            releaseAction.TrySetResult(); releaseExecutor.TrySetResult();
            session.Dispose(); await session.Released.WaitAsync(TimeSpan.FromSeconds(3)); await execution;
        }
    }

    [Fact]
    public async Task Scoped_abort_freezes_admitted_existing_control_before_publishing_Aborted()
    {
        var entered = Signal(); var release = Signal();
        var raw = new ProbeAxisTransport(); var config = new BindAxisSetting();
        var session = new AxisSession(new AxisController(raw, config), config, default, (_, _) => { });
        var (context, _) = TestContext.Create(new ReadStatePlugin(), new ReadStateSetting());
        var create = typeof(AxisSession).Assembly.GetType("PluginCommon.ExecutionScopedExecutor", true)!
            .GetMethod("Create", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var factory = new Func<IStepExecutor>(() => new CallbackExecutor(async _ =>
        {
            await session.ExecuteAsync(async (_, _) => { entered.TrySetResult(); await release.Task; }, true, 20, default);
            return new() { StepResult = new() { Status = TestStatus.Completed } };
        }));
        var wrapper = (IStepExecutor)create.Invoke(null, ["CiA402", factory])!;
        using var caller = new CancellationTokenSource();
        var execution = wrapper.ExecuteAsync(context, caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await execution.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync((_, _) => Task.CompletedTask, false, 20, default));
            Assert.False(session.Released.IsCompleted); Assert.Equal(0, raw.DisposeCount);
        }
        finally
        {
            release.TrySetResult(); await session.Released.WaitAsync(TimeSpan.FromSeconds(3)); await execution;
        }
    }
    [Fact]
    public async Task Session_close_retires_front_wait_but_keeps_actual_action_and_axis_owned()
    {
        var entered = Signal(); var release = Signal();
        var raw = new ProbeAxisTransport();
        var config = new BindAxisSetting { CommunicationTimeoutMs = 60000, ShutdownTimeoutMs = 1000 };
        var session = new AxisSession(new AxisController(raw, config), config, default, (_, _) => { });
        var waiting = session.ExecuteAsync(async (_, _) => { entered.TrySetResult(); await release.Task; }, false, 20, default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            session.Dispose();
            Assert.Same(waiting, await Task.WhenAny(waiting, Task.Delay(400)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => waiting);
            Assert.False(session.Released.IsCompleted);
            Assert.Equal(0, raw.DisposeCount);
        }
        finally
        {
            release.TrySetResult();
            try { await waiting; } catch (Exception) { }
            await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
        }
        Assert.Equal(1, raw.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Caller_cancel_is_fast_but_transport_keeps_gate_until_native_callbacks_exit(bool failBoth)
    {
        var entered = Signal(); var callbackEntered = Signal(); var callbackExit = Signal();
        var nativeReturn = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = new ProbeAxisTransport { ReadHandler = (index, ct) =>
        {
            _ = ct.Register(() =>
            {
                callbackEntered.TrySetResult(); callbackExit.Task.GetAwaiter().GetResult();
                if (failBoth) throw new InvalidOperationException("axis callback failure");
            });
            entered.TrySetResult(); return nativeReturn.Task;
        } };
        var transport = new CoordinatedAxisTransport(raw, 60000);
        using var caller = new CancellationTokenSource();
        var reading = transport.ReadObjectAsync(0x6041, 0, caller.Token);
        Task? cancelling = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            cancelling = Task.Run(caller.Cancel);
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Same(cancelling, await Task.WhenAny(cancelling, Task.Delay(400)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(1)));
            transport.Dispose();
            if (failBoth) nativeReturn.TrySetException(new InvalidOperationException("axis native failure"));
            else nativeReturn.TrySetResult([0, 0]);
            await Task.Delay(50);
            Assert.Equal(0, raw.DisposeCount);
        }
        finally
        {
            callbackExit.TrySetResult(); nativeReturn.TrySetResult([0, 0]);
            if (cancelling != null) await cancelling;
            try { await reading; } catch (Exception) { }
            if (!failBoth) await transport.DisposeAsync();
            else
            {
                var error = await Assert.ThrowsAsync<AggregateException>(() => transport.DisposeAsync().AsTask());
                Assert.Contains("native failure", error.ToString()); Assert.Contains("callback failure", error.ToString());
            }
        }
        Assert.False(raw.DisposedDuringCall);
    }

    [Fact]
    public async Task Internal_deadline_keeps_native_cancel_callbacks_owned_after_native_body_returns()
    {
        var entered = Signal(); var callbackEntered = Signal(); var callbackExit = Signal();
        var nativeReturn = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = new ProbeAxisTransport { ReadHandler = (index, ct) =>
        {
            _ = ct.Register(() => { callbackEntered.TrySetResult(); callbackExit.Task.GetAwaiter().GetResult(); });
            entered.TrySetResult(); return nativeReturn.Task;
        } };
        var transport = new CoordinatedAxisTransport(raw, 100);
        var reading = transport.ReadObjectAsync(0x6041, 0, default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => reading.WaitAsync(TimeSpan.FromSeconds(1)));
            transport.Dispose(); nativeReturn.TrySetResult([0, 0]);
            await Task.Delay(50);
            Assert.Equal(0, raw.DisposeCount);
        }
        finally
        {
            callbackExit.TrySetResult(); nativeReturn.TrySetResult([0, 0]);
            try { await reading; } catch (TimeoutException) { }
            await transport.DisposeAsync();
        }
        Assert.False(raw.DisposedDuringCall);
    }

    [Fact]
    public async Task Transport_dispose_retires_active_and_queued_waiters_without_releasing_inner()
    {
        var entered = Signal(); var release = Signal();
        var raw = new ProbeAxisTransport { ReadHandler = async (_, _) => { entered.TrySetResult(); await release.Task; return new byte[2]; } };
        var transport = new CoordinatedAxisTransport(raw, 60000);
        var first = transport.ReadObjectAsync(0x6041, 0, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var queued = transport.ReadObjectAsync(0x6061, 0, default);
        try
        {
            transport.Dispose();
            Assert.Same(first, await Task.WhenAny(first, Task.Delay(400)));
            Assert.Same(queued, await Task.WhenAny(queued, Task.Delay(400)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => first);
            await Assert.ThrowsAsync<InvalidOperationException>(() => queued);
            Assert.Equal(1, raw.Calls); Assert.Equal(0, raw.DisposeCount);
        }
        finally
        {
            release.TrySetResult();
            try { await first; } catch (Exception) { }
            try { await queued; } catch (Exception) { }
            await transport.DisposeAsync();
        }
        Assert.False(raw.DisposedDuringCall); Assert.Equal(1, raw.DisposeCount);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class CallbackExecutor(Func<CancellationToken, Task<ExecutionResult>> run) : IStepExecutor
    {
        public Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default) => run(cancellationToken);
    }
}
