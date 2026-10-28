using System.Collections.Concurrent;
using System.Text;
using SerialPort.Helpers;
using Xunit;

namespace SerialPort.ContractTests;

public class SerialTransactionTests
{
    [Fact]
    public async Task InvalidReadOptionsDoNotSendACommand()
    {
        var channel = new FakeChannel(); var resource = new SerialConnectionResource(channel);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => resource.QueryAsync([1], -1, "", 1000, default));
        Assert.Equal(0, channel.WriteCount);
        await resource.CloseAsync(1000, default);
    }
    [Fact]
    public async Task OneReadContainingTwoLinesPreservesSecondLine()
    {
        var channel = new FakeChannel(); channel.Received.Enqueue(Encoding.UTF8.GetBytes("A\nB\n"));
        var resource = new SerialConnectionResource(channel);
        Assert.Equal("A\n", Encoding.UTF8.GetString(await resource.ReadFrameAsync(0, "\\n", 1000, default)));
        Assert.Equal("B\n", Encoding.UTF8.GetString(await resource.ReadFrameAsync(0, "\\n", 1000, default)));
        Assert.Equal(1, channel.ReadCount);
        await resource.CloseAsync(1000, default);
    }

    [Fact]
    public async Task CancelledNativeReadCannotStealANewTransactionsResponse()
    {
        var channel = new FakeChannel { Block = true };
        var resource = new SerialConnectionResource(channel);
        using var cts = new CancellationTokenSource();
        var read = resource.QueryAsync([1], 1, "", 1000, cts.Token);
        await channel.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        await Assert.ThrowsAsync<InvalidOperationException>(() => resource.QueryAsync([2], 1, "", 1000, default));
        Assert.Equal(1, channel.WriteCount);
        var closing = resource.CloseAsync(1000, default);
        Assert.False(channel.Disposed);
        channel.Unblock.Set();
        await closing;
        Assert.True(channel.Disposed);
    }

    private sealed class FakeChannel : ISerialChannel
    {
        public ConcurrentQueue<byte[]> Received { get; } = new();
        public int ReadCount; public int WriteCount; public bool Block; public bool Disposed;
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Unblock { get; } = new();
        public bool IsOpen => !Disposed;
        public int ReadTimeout { get; set; } = 1000;
        public int WriteTimeout { get; set; } = 1000;
        public int Read(byte[] buffer, int offset, int count)
        {
            Interlocked.Increment(ref ReadCount); ReadStarted.TrySetResult();
            if (Block) { Unblock.Wait(); buffer[offset] = 42; return 1; }
            if (!Received.TryDequeue(out var bytes)) throw new TimeoutException();
            Array.Copy(bytes, 0, buffer, offset, bytes.Length); return bytes.Length;
        }
        public void Write(byte[] bytes) => Interlocked.Increment(ref WriteCount);
        public void Dispose() => Disposed = true;
    }
}
