using AuditTestSupport;
using SerialPort.Helpers;
using Xunit;
namespace SerialPort.ContractTests;
public class OpeningOwnershipTests
{
    [Fact]
    public async Task CloseCanFindAndFreezeAnInProgressOpen()
    {
        var context = ContextProxy.Create(); var channel = new Channel(); var resource = new SerialConnectionResource(channel);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var exit = new ManualResetEventSlim();
        var opening = SerialPortLifecycle.OpenRegisteredAsync(context, "P", resource, () => { entered.TrySetResult(); exit.Wait(); }, default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(context.Resources.TryGet<SerialConnectionResource>(SerialPortHelper.GetPortKey("P"), out var owner));
            Assert.Same(resource, owner);
            var closing = owner.CloseAsync(1000, default); exit.Set();
            await closing; await Assert.ThrowsAnyAsync<Exception>(() => opening);
            Assert.False(context.Resources.TryGet<SerialConnectionResource>(SerialPortHelper.GetPortKey("P"), out _));
        }
        finally { exit.Set(); await opening.ContinueWith(task => { _ = task.Exception; }); }
    }
    [Fact]
    public async Task FailedOpenAndFailedCleanupKeepARegisteredOwnerForRetry()
    {
        var context = ContextProxy.Create(); var channel = new Channel { FailClose = true }; var resource = new SerialConnectionResource(channel);
        await Assert.ThrowsAnyAsync<Exception>(() => SerialPortLifecycle.OpenRegisteredAsync(context, "P", resource, () => throw new InvalidOperationException("open"), default));
        Assert.True(context.Resources.TryGet<SerialConnectionResource>(SerialPortHelper.GetPortKey("P"), out var registered));
        Assert.Same(resource, registered);
        channel.FailClose = false; await resource.CloseAsync(1000, default);
    }
    private sealed class Channel : ISerialChannel
    {
        public bool FailClose;
        public bool IsOpen => false;
        public int ReadTimeout { get; set; } = 1000;
        public int WriteTimeout { get; set; } = 1000;
        public int Read(byte[] buffer, int offset, int count) => 0;
        public void Write(byte[] bytes) { }
        public void Dispose() { if (FailClose) throw new InvalidOperationException("close"); }
    }
}
