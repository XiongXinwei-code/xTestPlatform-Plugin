using System.IO;
using SerialPort.Helpers;
using Xunit;

namespace SerialPort.Tests;

public sealed class CloseOutcomeTests
{
    [Fact]
    public async Task Completed_interrupt_failure_does_not_fail_successful_dispose()
    {
        var channel = new Channel();
        var owner = new SerialConnectionResource(channel);

        await owner.CloseAsync(2000, default);
        await owner.CloseAsync(2000, default);

        Assert.True(owner.IsClosed);
        Assert.Equal(1, channel.Interrupts);
        Assert.Equal(1, channel.Disposes);
    }

    private sealed class Channel : ISerialChannel, IInterruptibleSerialChannel
    {
        internal int Interrupts, Disposes;
        public bool IsOpen => Disposes == 0;
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }
        public int Read(byte[] buffer, int offset, int count) => 0;
        public void Write(byte[] bytes) { }
        public void CancelPendingIo() { Interlocked.Increment(ref Interrupts); throw new IOException("interrupt returned failure"); }
        public void Dispose() => Interlocked.Increment(ref Disposes);
    }
}
