using System.Reflection;
using Ivi.Visa;
using VISA.Executors;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace VISA.Tests;

public sealed class ExecutionEpochTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_write_with_pending_name_expression_cannot_use_owner_from_next_sdk_execution(bool cleanupAll)
    {
        using var guard = new CancellationTokenSource(15000);
        var actual = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var name = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new VisaWriteExecutor((expression, _) =>
        {
            if (expression == "\"C\"") { entered.TrySetResult(); return name.Task; }
            return Task.FromResult(expression.Trim('"'));
        });
        var (context, state) = PublicationContextProxy.Create(new VisaWritePlugin(), new VisaWriteSetting { ConnectionName = "\"C\"", Command = "\"OUTP ON\"" });
        state.ResourcesOverride = actual;
        var execution = executor.ExecuteAsync(context, guard.Token);
        await entered.Task.WaitAsync(guard.Token);
        actual.GetType().GetMethod(cleanupAll ? "CleanupAll" : "CleanupExecution", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null);
        var session = DispatchProxy.Create<IMessageBasedSession, CounterSession>();
        var counter = (CounterSession)(object)session;
        var fresh = new VisaSessionResource(session, 1000, "\n");
        string key = VisaHelper.GetSessionKey("C"); actual.Set(key, fresh, ResourceLifetime.Execution);
        try
        {
            name.SetResult("C");
            Assert.Equal(TestStatus.Error, (await execution.WaitAsync(guard.Token)).StepResult.Status);
            Assert.False(guard.IsCancellationRequested); Assert.Equal(0, counter.Writes);
            Assert.True(actual.TryGet<VisaSessionResource>(key, out var current)); Assert.Same(fresh, current); Assert.False(fresh.IsClosed);
        }
        finally { name.TrySetResult("C"); await execution.WaitAsync(guard.Token); await fresh.CloseAsync(1000, default); actual.GetType().GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sdk_cleanup_freezes_old_Open_waiting_for_registration_and_first_new_execution_can_open(bool cleanupAll)
    {
        using var guard = new CancellationTokenSource(15000);
        var actual = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        int opens = 0;
        var executor = new VisaOpenExecutor((_, _) => { Interlocked.Increment(ref opens); return DispatchProxy.Create<IMessageBasedSession, PublicationBoundaryTests.ControlledSession>(); });
        var (context, state) = PublicationContextProxy.Create(new VisaOpenPlugin(), new VisaOpenSetting
        { ConnectionName = "\"C\"", ResourceString = "\"TCPIP::127.0.0.1::" + Random.Shared.Next(30000, 50000) + "::SOCKET\"", OpenTimeoutMs = 4000 });
        state.ResourcesOverride = actual;
        string key = VisaHelper.GetSessionKey("C");
        var gate = VisaHelper.GetRegistrationLock(actual, key); await gate.WaitAsync(guard.Token);
        var scheduler = new HeldContinuationContext();
        var opening = scheduler.Start(() => executor.ExecuteAsync(context, guard.Token));
        bool gateHeld = true;
        try
        {
            // Observe the actual SemaphoreSlim async queue, not an arbitrary delay.
            var head = typeof(SemaphoreSlim).GetField("m_asyncHead", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? typeof(SemaphoreSlim).GetField("_asyncHead", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Test runtime has no async-waiter field");
            while (head.GetValue(gate) == null)
            {
                guard.Token.ThrowIfCancellationRequested();
                Assert.False(opening.IsCompleted);
                if (!scheduler.RunOne()) await Task.Delay(2, guard.Token);
            }
            actual.GetType().GetMethod(cleanupAll ? "CleanupAll" : "CleanupExecution", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null);
            gate.Release(); gateHeld = false;
            await scheduler.DrainUntilAsync(opening, guard.Token);
            Assert.False(guard.IsCancellationRequested);
            Assert.Equal(TestStatus.Error, (await opening).StepResult.Status);
            Assert.Equal(0, opens);
            Assert.False(actual.TryGet<VisaSessionResource>(key, out _));

            Assert.Equal(TestStatus.Completed, (await executor.ExecuteAsync(context, guard.Token).WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(1, opens);
            Assert.True(actual.TryGet<VisaSessionResource>(key, out var current)); Assert.False(current.IsClosed);
        }
        finally
        {
            if (gateHeld) gate.Release();
            await scheduler.DrainUntilAsync(opening, guard.Token);
            if (actual.TryGet<VisaSessionResource>(key, out var owner)) await owner.CloseAsync(2000, default);
            actual.GetType().GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null);
        }
    }

    public class CounterSession : DispatchProxy
    {
        private object? _formatted;
        internal int Writes;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_FormattedIO")
            {
                if (_formatted == null) { _formatted = Create(method.ReturnType, typeof(CounterFormatted)); ((CounterFormatted)_formatted).Owner = this; }
                return _formatted;
            }
            return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
    public class CounterFormatted : DispatchProxy
    {
        internal CounterSession Owner = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "Write") Interlocked.Increment(ref Owner.Writes);
            return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
}
