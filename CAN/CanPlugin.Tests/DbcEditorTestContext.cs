using System.Threading.Channels;
using CAN.Dbc;
using CAN.UI.Dbc;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Tests;

// Drive only these fixtures' model publications and async continuations on the real STA.
// WPF controls still run synchronously on their owning thread; the host Dispatcher is not pumped.
internal sealed class DbcEditorTestContext : SynchronizationContext
{
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly Channel<Action> _callbacks = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions { SingleReader = true });

    internal DbcEditorViewModel CreateModel(IStepPlugin plugin) => new(plugin, DbcDocumentIO.ReadAsync, InvokeOnEditorAsync);

    internal static void Run(string scenario, TimeSpan timeout, Func<DbcEditorTestContext, Task> verify)
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("Actual DBC editor fixtures require an STA thread.");
        var ui = new DbcEditorTestContext();
        var previous = Current;
        SetSynchronizationContext(ui);
        try
        {
            using var deadline = new CancellationTokenSource(timeout);
            var test = verify(ui);
            // Also wake the reader if the scenario finishes on a worker without another STA continuation.
            _ = test.ContinueWith(_ => ui._callbacks.Writer.TryWrite(static () => { }), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            while (!test.IsCompleted)
            {
                Action callback;
                try
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    callback = ui._callbacks.Reader.ReadAsync(deadline.Token).AsTask().GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested)
                {
                    throw new TimeoutException($"{scenario} did not finish its STA callbacks within {timeout}.");
                }
                callback();
            }
            test.GetAwaiter().GetResult();
        }
        finally
        {
            ui._callbacks.Writer.TryComplete();
            SetSynchronizationContext(previous);
        }
    }

    public override SynchronizationContext CreateCopy() => this;

    public override void Post(SendOrPostCallback callback, object? state)
    {
        // A continuation arriving after the fixture timed out cannot run on a later test's STA.
        _callbacks.Writer.TryWrite(() => callback(state));
    }

    public override void Send(SendOrPostCallback callback, object? state) => InvokeOnEditorAsync(() => callback(state)).GetAwaiter().GetResult();

    internal Task InvokeOnEditorAsync(Action action)
    {
        if (Environment.CurrentManagedThreadId == _ownerThread) { action(); return Task.CompletedTask; }
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_callbacks.Writer.TryWrite(() =>
        {
            try { action(); finished.SetResult(); }
            catch (Exception ex) { finished.SetException(ex); }
        })) finished.SetException(new InvalidOperationException("The DBC editor test scope has ended."));
        return finished.Task;
    }
}
