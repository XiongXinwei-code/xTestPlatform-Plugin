using System.Windows.Threading;

namespace NiGrpc.Tests;

// Syncfusion 共享主题缓存绑定首个 Dispatcher；所有编辑测试复用一个真实 STA 调度线程。
internal static class UiThread
{
    private static readonly Lazy<Dispatcher> Owner = new(() =>
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { ready.SetResult(Dispatcher.CurrentDispatcher); Dispatcher.Run(); })
        { IsBackground = true, Name = "NiGrpc editor tests" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return ready.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    });

    public static void Run(Action action) => Owner.Value.Invoke(action);
    public static Task RunAsync(Func<Task> action) => Owner.Value.InvokeAsync(action).Task.Unwrap();
}
