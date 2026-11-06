using SerialPort.Executors;
using SerialPort.Helpers;
using SerialPort.Models;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace SerialPort.Tests;

public sealed class OpenRestartBoundaryTests
{
    // The guard bounds the test. These scenarios drive cancellation and close explicitly.
    private const int OpenTimeoutMs = 30_000;
    private static int _nextPort = 730_000;

    [Fact]
    public async Task Close_during_queued_restart_prevents_late_registration_of_that_open_generation()
    {
        using var guard = new CancellationTokenSource(15000); using var caller = new CancellationTokenSource(); using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); int opens = 0;
        string name = "COM" + Interlocked.Increment(ref _nextPort);
        var executor = new SerialPortOpenExecutor((port, _, _) => (new SerialConnectionResource(new Channel(), physicalPort: port), () =>
        {
            Interlocked.Increment(ref opens);
            caller.Cancel();
            entered.TrySetResult();
            release.Wait(guard.Token);
        }));
        var (context, state) = PublicationContextProxy.Create(new SerialPortOpenPlugin(), new SerialPortOpenSetting { PortName = "\"" + name + "\"", TimeoutMs = OpenTimeoutMs });
        // Warm expression compilation before exercising native Open/Close ordering.
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>("\"" + name + "\"", context);
        string key = SerialPortHelper.GetPortKey(name);
        var first = executor.ExecuteAsync(context, caller.Token);
        Task<ExecutionResult>? restarting = null, closing = null;
        try
        {
            await entered.Task.WaitAsync(guard.Token);
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
            await ReleaseAndCloseAsync(release, context, key, first, restarting, closing);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Interrupted_open_preserves_cancellation_and_first_same_registry_restart(bool restart, bool restartBeforeExit)
    {
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int opens = 0;
        string name = "COM" + Interlocked.Increment(ref _nextPort);
        var executor = new SerialPortOpenExecutor((port, _, _) =>
        {
            var channel = new Channel();
            return (new SerialConnectionResource(channel, physicalPort: port), () =>
            {
                if (Interlocked.Increment(ref opens) == 1)
                {
                    caller.Cancel();
                    entered.TrySetResult();
                    release.Wait(guard.Token);
                }
            });
        });
        var (context, state) = PublicationContextProxy.Create(new SerialPortOpenPlugin(), new SerialPortOpenSetting { PortName = "\"" + name + "\"", TimeoutMs = OpenTimeoutMs });
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>("\"" + name + "\"", context);
        string key = SerialPortHelper.GetPortKey(name);
        var first = executor.ExecuteAsync(context, caller.Token);
        Task<ExecutionResult>? queuedRestart = null;
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            var old = Assert.IsType<SerialConnectionResource>(state.Store.Values[key]);
            var interrupted = await first.WaitAsync(guard.Token);
            Assert.Equal(TestStatus.Aborted, interrupted.StepResult.Status);
            Assert.Same(old, state.Store.Values[key]);
            Assert.False(old.IsClosed);
            if (restartBeforeExit)
            {
                var lookup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                state.Store.OnTryGet = (port, type) => { if (port == key && type == typeof(object)) lookup.TrySetResult(); };
                queuedRestart = executor.ExecuteAsync(context, guard.Token);
                await lookup.Task.WaitAsync(guard.Token);
                Assert.False(queuedRestart.IsCompleted);
                Assert.Equal(1, opens);
            }
            release.Set();
            await old.CloseAsync(2000, default);
            if (restart)
            {
                var restarted = await (queuedRestart ?? executor.ExecuteAsync(context, guard.Token));
                Assert.Equal(TestStatus.Completed, restarted.StepResult.Status);
                Assert.Equal(2, opens);
                Assert.NotSame(old, state.Store.Values[key]);
            }
        }
        finally
        {
            await ReleaseAndCloseAsync(release, context, key, first, queuedRestart);
        }
    }

    private static async Task ReleaseAndCloseAsync(ManualResetEventSlim release, IExecutionContext context, string key, params Task?[] operations)
    {
        release.Set();
        try
        {
            await Task.WhenAll(operations.OfType<Task>()).WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            if (context.Resources.TryGet<SerialConnectionResource>(key, out var owner))
                await owner.CloseAsync(5_000, CancellationToken.None);
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
