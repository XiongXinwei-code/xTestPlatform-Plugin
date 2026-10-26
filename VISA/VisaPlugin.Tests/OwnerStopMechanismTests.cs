using System.Reflection;
using System.IO;
using Ivi.Visa;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace VISA.Tests;

public sealed class OwnerStopMechanismTests
{
    [Fact]
    public async Task SlowCancellationReportCannotKeepAlreadyExitedNativeSessionOccupied()
    {
        var session = DispatchProxy.Create<IMessageBasedSession, ResourceTests.SessionProxy>();
        var reporting = Signal(); var reportExit = Signal(); var reportExited = Signal();
        var owner = new VisaSessionResource(1000, "\n", _ =>
        {
            reporting.TrySetResult();
            try { reportExit.Task.GetAwaiter().GetResult(); }
            finally { reportExited.TrySetResult(); }
        });
        await owner.InitializeAsync(() => session, 3000, default);
        using var caller = new CancellationTokenSource();
        var entered = Signal(); var nativeExit = Signal();
        CancellationTokenRegistration registration = default;
        var reading = owner.RunAsync<int>(async (_, token) =>
        {
            registration = token.Register(() => throw new InvalidOperationException("slow-report callback failure"));
            entered.TrySetResult(); await nativeExit.Task; throw new IOException("slow-report native failure");
        }, -1, "收尾日志不能占用原生门", caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
            nativeExit.TrySetResult(); await reporting.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(reportExited.Task.IsCompleted);
            await owner.CloseAsync(300);
            Assert.True(owner.IsClosed); Assert.False(owner.HasPendingOperation);
            Assert.Equal(1, ((ResourceTests.SessionProxy)(object)session).Disposals);
            Assert.Contains("slow-report native failure", owner.LastCancellationError!.ToString());
            Assert.Contains("slow-report callback failure", owner.LastCancellationError.ToString());
        }
        finally
        {
            nativeExit.TrySetResult(); reportExit.TrySetResult();
            await reportExited.Task.WaitAsync(TimeSpan.FromSeconds(3));
            try { await reading.WaitAsync(TimeSpan.FromSeconds(3)); } catch (Exception) when (reading.IsCompleted) { }
            await owner.CloseAsync(3000); registration.Dispose();
        }
    }

    [Theory]
    [InlineData("CleanupExecution")]
    [InlineData("CleanupAll")]
    public async Task ActualSdkCleanupEndsReadObserversButReopenWaitsForRealNativeExit(string cleanup)
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var native = DispatchProxy.Create<IMessageBasedSession, PublicationBoundaryTests.ControlledSession>();
        var control = (PublicationBoundaryTests.ControlledSession)(object)native;
        string physical = "TCPIP0::owner-stop-" + Guid.NewGuid().ToString("N") + "::1000::SOCKET";
        var owner = new VisaSessionResource(native, -1, "\n", physical);
        var next = new VisaSessionResource(1000, "\n", resourceString: physical);
        var plugin = new VisaReadPlugin();
        var (context, state) = PublicationContextProxy.Create(plugin, new VisaReadSetting
        { ConnectionName = "\"C\"", ResultVariable = "Locals.rx" });
        state.ResourcesOverride = registry; Assert.True(registry.TryAdd(VisaHelper.GetSessionKey("C"), owner));
        var first = plugin.CreateExecutor().ExecuteAsync(context); Task? queued = null, reopening = null;
        int opens = 0;
        try
        {
            await control.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var second = plugin.CreateExecutor().ExecuteAsync(context); queued = second;
            registry.GetType().GetMethod(cleanup, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
            Assert.Equal(TestStatus.Error, (await first.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            Assert.Equal(TestStatus.Error, (await second.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            Assert.True(owner.HasPendingOperation); Assert.False(owner.IsClosed); Assert.Empty(state.Published);
            reopening = next.InitializeAsync(() =>
            { Interlocked.Increment(ref opens); return DispatchProxy.Create<IMessageBasedSession, ResourceTests.SessionProxy>(); }, 3000, default);
            Assert.Equal(0, opens); Assert.False(reopening.IsCompleted);
            control.Release.Set(); await reopening.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(owner.IsClosed); Assert.Equal(1, opens); Assert.Empty(state.Published);
        }
        finally
        {
            control.Release.Set(); await first.WaitAsync(TimeSpan.FromSeconds(3));
            if (queued != null) await queued.WaitAsync(TimeSpan.FromSeconds(3));
            if (reopening != null) await reopening.WaitAsync(TimeSpan.FromSeconds(3));
            await owner.CloseAsync(3000); await next.CloseAsync(3000);
            registry.GetType().GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
        }
    }

    [Fact]
    public async Task NativeCancellationFailureRemainsDiagnosableAfterCallerLeaves()
    {
        var session = DispatchProxy.Create<IMessageBasedSession, ResourceTests.SessionProxy>();
        var reports = new List<Exception>();
        var reported = Signal();
        var owner = new VisaSessionResource(1000, "\n", ex => { reports.Add(ex); reported.TrySetResult(); });
        await owner.InitializeAsync(() => session, 3000, default);
        using var caller = new CancellationTokenSource();
        var entered = Signal(); var callbackEntered = Signal(); var actualExit = Signal();
        CancellationTokenRegistration registration = default;
        var request = owner.RunAsync<int>(async (_, token) =>
        {
            registration = token.Register(() =>
            { callbackEntered.TrySetResult(); throw new InvalidOperationException("native VISA cancellation failed"); });
            entered.TrySetResult(); await actualExit.Task; throw new IOException("native VISA request failed");
        }, -1, "取消异常保留", caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(owner.HasPendingOperation);
            actualExit.TrySetResult(); await owner.CloseAsync(3000);
            Assert.Contains("native VISA cancellation failed", owner.LastCancellationError!.ToString());
            Assert.Contains("native VISA request failed", owner.LastCancellationError.ToString());
            await reported.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Single(reports); Assert.Equal(1, ((ResourceTests.SessionProxy)(object)session).Disposals);
            Assert.Contains("native VISA request failed", reports[0].ToString());
            Assert.Contains("native VISA cancellation failed", reports[0].ToString());
        }
        finally
        {
            actualExit.TrySetResult();
            try { await request.WaitAsync(TimeSpan.FromSeconds(3)); } catch (Exception) when (request.IsCompleted) { }
            await owner.CloseAsync(3000); registration.Dispose();
        }
    }

    [Theory]
    [InlineData("dispose")]
    [InlineData("close")]
    [InlineData("caller")]
    public async Task StoppingObserverDoesNotRunNativeCancellationCallbacksOnItsThread(string stop)
    {
        var session = DispatchProxy.Create<IMessageBasedSession, ResourceTests.SessionProxy>();
        var native = (ResourceTests.SessionProxy)(object)session;
        var owner = new VisaSessionResource(session, -1, "\n");
        using var caller = new CancellationTokenSource();
        using var callbackExit = new ManualResetEventSlim();
        var entered = Signal(); var callbackEntered = Signal(); var actualExit = Signal(); var stopReturned = Signal();
        CancellationTokenRegistration registration = default;
        Task? closing = null;
        var request = owner.RunAsync(async (_, token) =>
        {
            registration = token.Register(() => { callbackEntered.TrySetResult(); callbackExit.Wait(); });
            entered.TrySetResult();
            await actualExit.Task;
            return 7;
        }, -1, "受控原生调用", caller.Token);
        Task? stopping = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            stopping = Task.Run(() =>
            {
                if (stop == "caller") caller.Cancel();
                else if (stop == "close") closing = owner.CloseAsync(10000);
                else owner.Dispose();
                stopReturned.TrySetResult();
            });
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            // The callback remains behind a barrier; the stop thread and observer must already return.
            await stopReturned.Task.WaitAsync(TimeSpan.FromSeconds(1));
            if (stop == "caller") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            else await Assert.ThrowsAsync<InvalidOperationException>(() => request);
            Assert.True(owner.HasPendingOperation); Assert.False(owner.IsClosed); Assert.Equal(0, native.Disposals);
            actualExit.TrySetResult();
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(40));
            Assert.True(owner.HasPendingOperation); Assert.Equal(0, native.Disposals);
        }
        finally
        {
            actualExit.TrySetResult(); callbackExit.Set();
            if (stopping != null) await stopping.WaitAsync(TimeSpan.FromSeconds(3));
            try { await request.WaitAsync(TimeSpan.FromSeconds(3)); } catch (Exception) when (request.IsCompleted) { }
            if (closing != null) await closing.WaitAsync(TimeSpan.FromSeconds(3));
            await owner.CloseAsync(3000);
            registration.Dispose();
        }
        Assert.True(owner.IsClosed); Assert.Equal(1, native.Disposals);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
