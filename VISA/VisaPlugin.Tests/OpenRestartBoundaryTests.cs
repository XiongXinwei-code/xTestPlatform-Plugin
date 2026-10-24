using System.Reflection;
using Ivi.Visa;
using VISA.Executors;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Models;
using Xunit;

namespace VISA.Tests;

public sealed class OpenRestartBoundaryTests
{
    [Fact]
    public async Task Interrupted_pending_open_is_aborted_and_keeps_native_owner_until_exit()
    {
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var native = DispatchProxy.Create<IMessageBasedSession, PublicationBoundaryTests.ControlledSession>();
        var executor = new VisaOpenExecutor((_, _) =>
        {
            entered.TrySetResult();
            if (!release.Wait(10000)) throw new TimeoutException("Test guard: open was not released");
            return native;
        });
        var (context, state) = PublicationContextProxy.Create(new VisaOpenPlugin(), new VisaOpenSetting { ConnectionName = "\"C\"", ResourceString = "\"TCPIP::127.0.0.1::" + Random.Shared.Next(30000, 50000) + "::SOCKET\"", OpenTimeoutMs = 300 });
        var execution = executor.ExecuteAsync(context, caller.Token);
        VisaSessionResource? owner = null;
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            owner = Assert.IsType<VisaSessionResource>(state.Store.Values[VisaHelper.GetSessionKey("C")]);
            caller.Cancel();
            var result = await execution.WaitAsync(guard.Token);
            Assert.Same(owner, state.Store.Values[VisaHelper.GetSessionKey("C")]);
            Assert.False(owner.IsClosed);
            Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
        }
        finally
        {
            release.Set();
            await execution.WaitAsync(guard.Token);
            if (owner != null) await owner.CloseAsync(2000, default);
        }
    }

    [Fact]
    public async Task Cancelled_open_continuation_cannot_publish_terminator_or_success()
    {
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var native = DispatchProxy.Create<IMessageBasedSession, PublicationBoundaryTests.ControlledSession>();
        var executor = new VisaOpenExecutor((_, _) =>
        {
            entered.TrySetResult();
            if (!release.Wait(10000)) throw new TimeoutException("Test guard: open was not released");
            return native;
        });
        var (context, state) = PublicationContextProxy.Create(new VisaOpenPlugin(), new VisaOpenSetting { ConnectionName = "\"C\"", ResourceString = "\"TCPIP::127.0.0.1::55555::SOCKET\"", OpenTimeoutMs = 3000 });
        var continuation = new HeldContinuationContext();
        var execution = continuation.Start(() => executor.ExecuteAsync(context, caller.Token));
        try
        {
            await continuation.PumpUntilAsync(entered.Task, execution, guard.Token);
            release.Set();
            await continuation.WaitForContinuationAsync(guard.Token);
            caller.Cancel();
            await continuation.DrainUntilAsync(execution, guard.Token);
            Assert.Equal(TestStatus.Aborted, (await execution).StepResult.Status);
            Assert.False(state.Store.Values.ContainsKey(VisaHelper.GetTerminatorKey("C")));
        }
        finally
        {
            release.Set();
            await continuation.DrainUntilAsync(execution, guard.Token);
            if (state.Store.Values.TryGetValue(VisaHelper.GetSessionKey("C"), out var current) && current is VisaSessionResource owner)
                await owner.CloseAsync(2000, default);
        }
    }
}
