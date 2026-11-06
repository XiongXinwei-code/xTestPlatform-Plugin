using System.IO;
using System.Windows.Threading;
using Xunit;

namespace CAN.Tests;

public sealed class DbcEditorTestContextTests
{
    [StaFact]
    public void Worker_publications_and_awaits_return_to_the_owner_without_pumping_host_messages()
    {
        int owner = Environment.CurrentManagedThreadId;
        var previous = SynchronizationContext.Current;
        bool unrelatedMessageRan = false;
        var queued = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => unrelatedMessageRan = true));
        try
        {
            DbcEditorTestContext.Run("isolated STA", TimeSpan.FromSeconds(5), async ui =>
            {
                await Task.Run(() => ui.InvokeOnEditorAsync(() => Assert.Equal(owner, Environment.CurrentManagedThreadId)));
                await Task.Yield();
                Assert.Equal(owner, Environment.CurrentManagedThreadId);
                Assert.Same(ui, SynchronizationContext.Current);
                Assert.False(unrelatedMessageRan);
            });
            Assert.Equal(DispatcherOperationStatus.Pending, queued.Status);
            Assert.Same(previous, SynchronizationContext.Current);
        }
        finally { queued.Abort(); }
    }

    [StaFact]
    public void Callback_errors_and_missing_completions_reach_the_test_and_restore_its_context()
    {
        var previous = SynchronizationContext.Current;
        var expected = new IOException("failed DBC fixture publication");
        var error = Assert.Throws<IOException>(() => DbcEditorTestContext.Run("failed publication", TimeSpan.FromSeconds(5), ui =>
            Task.Run(() => ui.InvokeOnEditorAsync(() => throw expected))));
        Assert.Same(expected, error);
        Assert.Same(previous, SynchronizationContext.Current);

        var missing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var timeout = Assert.Throws<TimeoutException>(() => DbcEditorTestContext.Run("missing completion", TimeSpan.FromMilliseconds(100), _ => missing.Task));
            Assert.Contains("missing completion", timeout.Message);
            Assert.Same(previous, SynchronizationContext.Current);
        }
        finally { missing.TrySetResult(); }
    }
}
