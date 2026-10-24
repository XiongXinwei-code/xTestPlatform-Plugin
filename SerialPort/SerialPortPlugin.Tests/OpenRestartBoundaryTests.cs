using SerialPort.Executors;
using SerialPort.Helpers;
using SerialPort.Models;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Engine;
using Xunit;

namespace SerialPort.Tests;

public sealed class OpenRestartBoundaryTests
{
    [Fact]
    public async Task Close_during_queued_restart_prevents_late_registration_of_that_open_generation()
    {
        using var guard = new CancellationTokenSource(15000); using var caller = new CancellationTokenSource(); using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); int opens = 0;
        string name = "COM" + Random.Shared.Next(30000, 50000);
        var executor = new SerialPortOpenExecutor((port, _, _) => (new SerialConnectionResource(new Channel(), physicalPort: port), () =>
        {
            Interlocked.Increment(ref opens); entered.TrySetResult();
            if (!release.Wait(10000)) throw new TimeoutException("Test guard: open was not released");
        }));
        var (context, state) = PublicationContextProxy.Create(new SerialPortOpenPlugin(), new SerialPortOpenSetting { PortName = "\"" + name + "\"", TimeoutMs = 2000 });
        string key = SerialPortHelper.GetPortKey(name);
        var first = executor.ExecuteAsync(context, caller.Token);
        Task<ExecutionResult>? restarting = null, closing = null;
        try
        {
            await entered.Task.WaitAsync(guard.Token); caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await first.WaitAsync(guard.Token)).StepResult.Status);
            var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            state.Store.OnTryGet = (port, type) => { if (port == key && type == typeof(object)) captured.TrySetResult(); };
            restarting = executor.ExecuteAsync(context, guard.Token); await captured.Task.WaitAsync(guard.Token);
            long generation = SerialPortHelper.GetCloseGeneration(context.Resources, key);
            var (closeContext, _) = PublicationContextProxy.Create(new SerialPortClosePlugin(), new SerialPortCloseSetting { PortName = "\"" + name + "\"", TimeoutMs = 2000 }, state.Store);
            closing = new SerialPortCloseExecutor().ExecuteAsync(closeContext, guard.Token);
            while (SerialPortHelper.GetCloseGeneration(context.Resources, key) == generation) await Task.Delay(2, guard.Token);
            release.Set();
            Assert.Equal(TestStatus.Error, (await restarting.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(TestStatus.Completed, (await closing.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(1, opens); Assert.False(state.Store.Values.ContainsKey(key));
        }
        finally
        {
            release.Set(); await first.WaitAsync(guard.Token);
            if (restarting != null) await restarting.WaitAsync(guard.Token);
            if (closing != null) await closing.WaitAsync(guard.Token);
            if (state.Store.Values.TryGetValue(key, out var current) && current is SerialConnectionResource owner) await owner.CloseAsync(2000, default);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Interrupted_open_preserves_cancellation_and_first_same_registry_restart(bool checkCancellation, bool restartBeforeExit)
    {
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int opens = 0;
        string name = "COM" + Random.Shared.Next(30000, 50000);
        var executor = new SerialPortOpenExecutor((port, _, _) =>
        {
            var channel = new Channel();
            return (new SerialConnectionResource(channel, physicalPort: port), () =>
            {
                if (Interlocked.Increment(ref opens) == 1)
                {
                    entered.TrySetResult();
                    if (!release.Wait(10000)) throw new TimeoutException("Test guard: open was not released");
                }
            });
        });
        var (context, state) = PublicationContextProxy.Create(new SerialPortOpenPlugin(), new SerialPortOpenSetting { PortName = "\"" + name + "\"", TimeoutMs = 300 });
        string key = SerialPortHelper.GetPortKey(name);
        var first = executor.ExecuteAsync(context, caller.Token);
        SerialConnectionResource? old = null;
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            old = Assert.IsType<SerialConnectionResource>(state.Store.Values[key]);
            caller.Cancel();
            var interrupted = await first.WaitAsync(guard.Token);
            Assert.Same(old, state.Store.Values[key]);
            Assert.False(old.IsClosed);
            if (checkCancellation) Assert.Equal(TestStatus.Aborted, interrupted.StepResult.Status);
            Task<ExecutionResult>? queuedRestart = restartBeforeExit ? executor.ExecuteAsync(context, guard.Token) : null;
            if (queuedRestart != null)
            {
                await Task.WhenAny(queuedRestart, Task.Delay(40, guard.Token));
                Assert.False(queuedRestart.IsCompleted);
                Assert.Equal(1, opens);
            }
            release.Set();
            await old.CloseAsync(2000, default);
            if (!checkCancellation)
            {
                var restarted = await (queuedRestart ?? executor.ExecuteAsync(context, guard.Token));
                Assert.Equal(TestStatus.Completed, restarted.StepResult.Status);
                Assert.Equal(2, opens);
                Assert.NotSame(old, state.Store.Values[key]);
            }
        }
        finally
        {
            release.Set();
            await first.WaitAsync(guard.Token);
            if (old != null) await old.CloseAsync(2000, default);
            if (state.Store.Values.TryGetValue(key, out var current) && current is SerialConnectionResource owner)
                await owner.CloseAsync(2000, default);
        }
    }
    private sealed class Channel : ISerialChannel
    {
        public bool IsOpen { get; private set; } = true;
        public int ReadTimeout { get; set; } = 1000;
        public int WriteTimeout { get; set; } = 1000;
        public int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public void Write(byte[] bytes) { }
        public void Dispose() => IsOpen = false;
    }
}
