using System.Reflection;
using System.IO;
using Ivi.Visa;
using VISA.Helpers;
using Xunit;

namespace VISA.Tests;

public sealed class ImmediateRerunTests
{
    [Fact]
    public async Task NewExecutionWaitsForRetiringPhysicalResourceBeforeNativeOpen()
    {
        string physical = "TCPIP0::host" + Guid.NewGuid().ToString("N") + "::INSTR";
        var session = DispatchProxy.Create<IMessageBasedSession, ResourceTests.SessionProxy>();
        var proxy = (ResourceTests.SessionProxy)(object)session;
        var old = new VisaSessionResource(session, 1000, "\n", physical);
        var next = new VisaSessionResource(1000, "\n", resourceString: physical.ToLowerInvariant());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(); using var cancel = new CancellationTokenSource();
        var pending = old.RunBlockingAsync(_ => { entered.TrySetResult(); release.Wait(); return 1; }, 2000, "old read", cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending); old.Dispose();
        bool opened = false;
        var opening = next.InitializeAsync(() =>
        {
            if (proxy.Disposals == 0) throw new IOException("VISA native handle busy");
            opened = true; return DispatchProxy.Create<IMessageBasedSession, ResourceTests.SessionProxy>();
        }, 2000, default);
        try
        {
            await Task.WhenAny(opening, Task.Delay(100)); Assert.False(opening.IsCompleted);
            release.Set(); await opening; Assert.True(opened);
        }
        finally { release.Set(); await old.CloseAsync(2000); await next.CloseAsync(2000); }
    }
}
