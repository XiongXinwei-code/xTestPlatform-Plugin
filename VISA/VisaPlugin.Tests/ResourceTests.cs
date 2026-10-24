using System.Reflection;
using Ivi.Visa;
using VISA.Helpers;
using Xunit;

namespace VISA.Tests;

public sealed class ResourceTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("10")]
    [InlineData("error 1")]
    public void OpcRejectsAnythingExceptCompletionOne(string response) =>
        Assert.Throws<InvalidOperationException>(() => VisaHelper.ValidateOpcResponse(response));

    [Fact]
    public void OpcAcceptsWhitespaceAroundOne() => VisaHelper.ValidateOpcResponse(" 1\r\n");

    [Fact]
    public async Task CancelledNativeCallRetainsOwnershipAndCloseWaitsForRealExit()
    {
        var session = DispatchProxy.Create<IMessageBasedSession, SessionProxy>();
        var proxy = (SessionProxy)(object)session;
        var resource = new VisaSessionResource(session, 1000, "\n");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var cts = new CancellationTokenSource();
        var pending = resource.RunBlockingAsync(_ => { entered.TrySetResult(); release.Wait(TimeSpan.FromSeconds(5)); return 42; }, 1000, "fake native", cts.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.True(resource.HasPendingOperation);
            await Assert.ThrowsAsync<InvalidOperationException>(() => resource.RunBlockingAsync(_ => 7, 100, "new I/O", default));
            await Assert.ThrowsAsync<TimeoutException>(() => resource.CloseAsync(20));
            Assert.Equal(0, proxy.Disposals);
        }
        finally { release.Set(); await resource.CloseAsync(2000); }
        Assert.True(resource.IsClosed); Assert.Equal(1, proxy.Disposals);
    }

    [Fact]
    public async Task TimeoutPropertyFaultCannotLeakTransactionLock()
    {
        var session = DispatchProxy.Create<IMessageBasedSession, SessionProxy>();
        var proxy = (SessionProxy)(object)session; proxy.ThrowOnGet = true;
        var resource = new VisaSessionResource(session, 1000, "\n");
        await Assert.ThrowsAsync<InvalidOperationException>(() => resource.RunBlockingAsync(s => VisaHelper.WithTemporaryTimeout(s, 20, () => 1), 1000, "timeout getter", default));
        await resource.CloseAsync(1000);
        Assert.Equal(1, proxy.Disposals);
    }

    [Fact]
    public void OriginalAndRestoreFailuresAreBothPreserved()
    {
        var session = DispatchProxy.Create<IMessageBasedSession, SessionProxy>();
        var proxy = (SessionProxy)(object)session; proxy.ThrowOnRestore = true;
        var ex = Assert.Throws<AggregateException>(() => VisaHelper.WithTemporaryTimeout<int>(session, 20,
            () => throw new InvalidOperationException("original I/O")));
        Assert.Contains(ex.InnerExceptions, e => e.Message == "original I/O");
        Assert.Contains(ex.InnerExceptions, e => e.Message == "restore timeout");
    }

    [Fact]
    public async Task CancelledOpenStillOwnsAndClosesTheLateSession()
    {
        var session = DispatchProxy.Create<IMessageBasedSession, SessionProxy>();
        var proxy = (SessionProxy)(object)session;
        var resource = new VisaSessionResource(1000, "\n");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(); using var cancellation = new CancellationTokenSource();
        var opening = resource.InitializeAsync(() => { entered.TrySetResult(); release.Wait(TimeSpan.FromSeconds(5)); return session; }, 1000, cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
            await Assert.ThrowsAsync<TimeoutException>(() => resource.CloseAsync(20));
            Assert.Equal(0, proxy.Disposals);
        }
        finally { release.Set(); await resource.CloseAsync(2000); }
        Assert.Equal(1, proxy.Disposals);
    }

    [Fact]
    public async Task FailedNativeDisposeRetainsHandleForConfirmedRetry()
    {
        var session = DispatchProxy.Create<IMessageBasedSession, SessionProxy>();
        var proxy = (SessionProxy)(object)session; proxy.FailDisposals = 1;
        var resource = new VisaSessionResource(session, 1000, "\n");
        await Assert.ThrowsAsync<InvalidOperationException>(() => resource.CloseAsync(1000));
        Assert.False(resource.IsClosed);
        await resource.CloseAsync(1000);
        Assert.True(resource.IsClosed); Assert.Equal(2, proxy.Disposals);
    }

    [Fact]
    public async Task RegistrationWaitHasItsOwnDeadlineDuringAnInfiniteOpen()
    {
        using var gate = new SemaphoreSlim(0, 1);
        await Assert.ThrowsAsync<TimeoutException>(() => VisaHelper.WaitForRegistrationAsync(gate, 20, default));
    }

    [Fact]
    public async Task CloseRequestedDuringOpenCannotReportThatSessionReady()
    {
        var session = DispatchProxy.Create<IMessageBasedSession, SessionProxy>();
        var resource = new VisaSessionResource(1000, "\n");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var opening = resource.InitializeAsync(() => { entered.TrySetResult(); release.Wait(TimeSpan.FromSeconds(5)); return session; }, 1000, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try { await Assert.ThrowsAsync<TimeoutException>(() => resource.CloseAsync(20)); }
        finally { release.Set(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => opening);
        await resource.CloseAsync(2000);
    }

    [Fact]
    public async Task ConfigurationAndDisposeFailureRetainAdoptedNativeHandle()
    {
        var session = DispatchProxy.Create<IMessageBasedSession, SessionProxy>();
        var proxy = (SessionProxy)(object)session; proxy.FailDisposals = 1;
        var resource = new VisaSessionResource(1000, "\n");
        await Assert.ThrowsAsync<InvalidOperationException>(() => resource.InitializeAsync(
            () => (IDisposable)session, _ => throw new InvalidOperationException("configure failed"), 1000, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => resource.CloseAsync(1000));
        Assert.False(resource.IsClosed);
        await resource.CloseAsync(1000);
        Assert.True(resource.IsClosed); Assert.Equal(2, proxy.Disposals);
    }

    public class SessionProxy : DispatchProxy
    {
        public int Disposals;
        public int FailDisposals;
        public bool ThrowOnGet, ThrowOnRestore;
        private int _timeout = 1000;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod!.Name)
            {
                case "Dispose":
                    if (Interlocked.Increment(ref Disposals) <= FailDisposals) throw new InvalidOperationException("dispose failed");
                    return null;
                case "get_TimeoutMilliseconds":
                    if (ThrowOnGet) throw new InvalidOperationException("timeout getter");
                    return _timeout;
                case "set_TimeoutMilliseconds":
                    var next = (int)args![0]!;
                    if (ThrowOnRestore && next == 1000) throw new InvalidOperationException("restore timeout");
                    _timeout = next; return null;
                default: throw new NotSupportedException(targetMethod.Name);
            }
        }
    }
}
