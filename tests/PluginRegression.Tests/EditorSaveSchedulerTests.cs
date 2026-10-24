using System.Reflection;
using Xunit;

namespace PluginRegression.Tests;

public sealed class EditorSaveSchedulerTests
{
    [StaFact]
    public void Flush_is_reentrant_safe_and_retains_failed_save_for_retry()
    {
        var type = typeof(Http.UI.ViewModels.HttpClientCreateViewModel).Assembly.GetType("PluginCommon.EditorSaveScheduler")!;
        var scheduler = Activator.CreateInstance(type)!; var queue = type.GetMethod("Queue")!; var flush = type.GetMethod("Flush")!;
        int saves = 0;
        queue.Invoke(scheduler, [(Action)(() => { if (++saves == 1) flush.Invoke(scheduler, null); })]);
        flush.Invoke(scheduler, null); Assert.Equal(1, saves);
        bool fail = true;
        queue.Invoke(scheduler, [(Action)(() => { if (fail) throw new InvalidOperationException("save failure"); saves++; })]);
        var error = Assert.Throws<TargetInvocationException>(() => flush.Invoke(scheduler, null));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.NotNull(type.GetProperty("LastError")!.GetValue(scheduler));
        fail = false; flush.Invoke(scheduler, null); Assert.Equal(2, saves);
        Assert.Null(type.GetProperty("LastError")!.GetValue(scheduler));
    }
}
