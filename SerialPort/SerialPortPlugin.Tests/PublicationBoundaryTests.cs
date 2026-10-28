using SerialPort.Helpers;
using SerialPort.Models;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace SerialPort.Tests;

public sealed class PublicationBoundaryTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task Read_does_not_publish_after_cancellation_or_owner_replacement(bool query, bool cancel)
    {
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        using var native = new ControlledChannel();
        using var nextChannel = new ControlledChannel();
        var old = new SerialConnectionResource(native);
        var fresh = new SerialConnectionResource(nextChannel);
        IStepPlugin plugin = query ? new SerialPortQueryPlugin() : new SerialPortReadPlugin();
        object setting = query
            ? new SerialPortQuerySetting { PortName = "\"COM1\"", WriteData = "\"01\"", ReadBytes = 1, Terminator = "", ResultVariable = "Locals.rx" }
            : new SerialPortReadSetting { PortName = "\"COM1\"", ReadBytes = 1, Terminator = "", ResultVariable = "Locals.rx" };
        var (context, state) = PublicationContextProxy.Create(plugin, setting);
        string key = SerialPortHelper.GetPortKey("COM1");
        state.Store.Values[key] = old;
        var continuation = new HeldContinuationContext();
        var execution = continuation.Start(() => plugin.CreateExecutor().ExecuteAsync(context, caller.Token));
        try
        {
            await continuation.PumpUntilAsync(native.Entered.Task, execution, guard.Token);
            native.Release.Set();
            await continuation.WaitForContinuationAsync(guard.Token);
            Assert.False(execution.IsCompleted);
            if (cancel) caller.Cancel(); else state.Store.Values[key] = fresh;
            await continuation.DrainUntilAsync(execution, guard.Token);
            var result = await execution;
            Assert.Equal(cancel ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            Assert.Empty(state.Published);
            if (!cancel) Assert.Same(fresh, state.Store.Values[key]);
            Assert.False(fresh.IsClosed);
        }
        finally
        {
            native.Release.Set();
            await continuation.DrainUntilAsync(execution, guard.Token);
            await old.CloseAsync(1000, default);
            await fresh.CloseAsync(1000, default);
        }
    }

    private sealed class ControlledChannel : ISerialChannel
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim Release = new();
        public bool IsOpen { get; private set; } = true;
        public int ReadTimeout { get; set; } = 3000;
        public int WriteTimeout { get; set; } = 3000;
        public int Read(byte[] buffer, int offset, int count)
        {
            Entered.TrySetResult();
            if (!Release.Wait(10000)) throw new TimeoutException("Test guard: controlled read was not released");
            buffer[offset] = 0x42; return 1;
        }
        public void Write(byte[] bytes) { }
        public void Dispose() { IsOpen = false; }
    }
}
