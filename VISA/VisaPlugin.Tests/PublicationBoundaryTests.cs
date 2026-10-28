using System.Reflection;
using Ivi.Visa;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace VISA.Tests;

public sealed class PublicationBoundaryTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task Read_does_not_publish_after_cancellation_or_owner_replacement(bool query, bool cancel)
    {
        using var guard = new CancellationTokenSource(15000);
        using var caller = new CancellationTokenSource();
        var native = DispatchProxy.Create<IMessageBasedSession, ControlledSession>();
        var control = (ControlledSession)(object)native;
        var old = new VisaSessionResource(native, 3000, "\n");
        var fresh = new VisaSessionResource(DispatchProxy.Create<IMessageBasedSession, ControlledSession>(), 3000, "\n");
        IStepPlugin plugin = query ? new VisaQueryPlugin() : new VisaReadPlugin();
        object setting = query
            ? new VisaQuerySetting { ConnectionName = "\"C\"", ResultVariable = "Locals.rx" }
            : new VisaReadSetting { ConnectionName = "\"C\"", ResultVariable = "Locals.rx" };
        var (context, state) = PublicationContextProxy.Create(plugin, setting);
        string key = VisaHelper.GetSessionKey("C");
        state.Store.Values[key] = old;
        var continuation = new HeldContinuationContext();
        var execution = continuation.Start(() => plugin.CreateExecutor().ExecuteAsync(context, caller.Token));
        try
        {
            await continuation.PumpUntilAsync(control.Entered.Task, execution, guard.Token);
            control.Release.Set();
            await continuation.WaitForContinuationAsync(guard.Token);
            Assert.False(execution.IsCompleted);
            if (cancel) caller.Cancel(); else state.Store.Values[key] = fresh;
            await continuation.DrainUntilAsync(execution, guard.Token);
            var result = await execution;
            Assert.Equal(cancel ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            Assert.Empty(state.Published);
            if (!cancel) Assert.Same(fresh, state.Store.Values[key]);
            Assert.False(fresh.IsClosed);
        }
        finally
        {
            control.Release.Set();
            await continuation.DrainUntilAsync(execution, guard.Token);
            await old.CloseAsync(1000, default);
            await fresh.CloseAsync(1000, default);
        }
    }

    public class ControlledSession : DispatchProxy
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim Release = new();
        private object? _formatted;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_FormattedIO")
            {
                if (_formatted == null)
                {
                    _formatted = DispatchProxy.Create(method.ReturnType, typeof(ControlledFormattedIo));
                    ((ControlledFormattedIo)_formatted).Session = this;
                }
                return _formatted;
            }
            return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
    public class ControlledFormattedIo : DispatchProxy
    {
        internal ControlledSession Session = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "ReadLine")
            {
                Session.Entered.TrySetResult();
                if (!Session.Release.Wait(10000)) throw new TimeoutException("Test guard: controlled read was not released");
                return "late";
            }
            return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
}
