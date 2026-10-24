using System.Reflection;
using Ivi.Visa;
using VISA.Helpers;
using Xunit;

namespace VISA.Tests;

public sealed class SubmissionBudgetTests
{
    [Theory]
    [InlineData("deadline")]
    [InlineData("close")]
    [InlineData("caller")]
    public async Task A_late_native_return_cannot_submit_the_next_command(string reason)
    {
        var session = DispatchProxy.Create<IMessageBasedSession, ResourceTests.SessionProxy>();
        var owner = new VisaSessionResource(session, 1000, "\n");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var caller = new CancellationTokenSource();
        int submitted = 0;
        var operation = owner.RunAsync(async (_, token) =>
        {
            Interlocked.Increment(ref submitted); entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("test fixture did not release native call");
            owner.ThrowIfSubmissionStopped();
            Interlocked.Increment(ref submitted);
            await Task.Delay(1, token);
            return submitted;
        }, reason == "deadline" ? 500 : 5000, "fake batch", caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            if (reason == "deadline") await Assert.ThrowsAsync<TimeoutException>(() => operation);
            else if (reason == "close") await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(20));
            else
            {
                caller.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
            }
            release.Set();
            Assert.NotNull(await Record.ExceptionAsync(async () => await operation));
            await owner.CloseAsync(2000);
            Assert.Equal(1, submitted);
        }
        finally { release.Set(); await owner.CloseAsync(3000); }
    }

    [Fact]
    public async Task Non_message_native_handle_is_owned_before_type_validation()
    {
        var handle = new NativeHandle();
        var owner = new VisaSessionResource(1000, "\n");
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InitializeAsync(
            () => handle, _ => { }, 1000, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CloseAsync(1000));
        Assert.False(owner.IsClosed);
        await owner.CloseAsync(1000);
        Assert.Equal(2, handle.Disposals);
    }

    private sealed class NativeHandle : IDisposable
    {
        internal int Disposals;
        public void Dispose()
        {
            if (++Disposals == 1) throw new InvalidOperationException("native close failed");
        }
    }
}
