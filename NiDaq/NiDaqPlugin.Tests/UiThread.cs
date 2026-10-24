using System.Windows.Threading;
namespace NiDaq.ContractTests;
internal static class UiThread
{
    private static readonly Lazy<Dispatcher> Owner = new(() =>
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { ready.SetResult(Dispatcher.CurrentDispatcher); Dispatcher.Run(); })
        { IsBackground = true, Name = "NiDaq editor tests" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return ready.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    });
    public static void Run(Action action) => Owner.Value.Invoke(action);
}
