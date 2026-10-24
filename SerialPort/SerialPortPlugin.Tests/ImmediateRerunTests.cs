using SerialPort.Helpers;
using System.IO;
using Xunit;

namespace SerialPort.ContractTests;

public sealed class ImmediateRerunTests
{
    [Fact]
    public async Task NewExecutionWaitsForRetiringPhysicalPortBeforeOpen()
    {
        var port = "COM" + Random.Shared.Next(10000, 20000);
        var raw = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var oldChannel = new Channel(); var nextChannel = new Channel();
        var old = new SerialConnectionResource(oldChannel, physicalPort: port);
        var next = new SerialConnectionResource(nextChannel, physicalPort: port);
        using var cancel = new CancellationTokenSource();
        oldChannel.ReadAction = () => { entered.TrySetResult(); release.Wait(); return 1; };
        var pending = old.ReadFrameAsync(1, "", 1000, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        old.Dispose();
        bool opened = false;
        var opening = next.OpenAsync(() => { if (!oldChannel.Released) throw new IOException("physical port busy"); opened = true; }, 2000, default);
        try
        {
            await Task.WhenAny(opening, Task.Delay(100));
            Assert.False(opening.IsCompleted);
            release.Set(); await opening;
            Assert.True(opened);
        }
        finally { release.Set(); await old.CloseAsync(2000, default); await next.CloseAsync(2000, default); }
    }

    private sealed class Channel : ISerialChannel
    {
        internal bool Released;
        internal Func<int>? ReadAction;
        public bool IsOpen => !Released;
        public int ReadTimeout { get; set; } = 1000;
        public int WriteTimeout { get; set; } = 1000;
        public int Read(byte[] buffer, int offset, int count) { buffer[offset] = 1; return ReadAction?.Invoke() ?? 1; }
        public void Write(byte[] bytes) { }
        public void Dispose() => Released = true;
    }
}
