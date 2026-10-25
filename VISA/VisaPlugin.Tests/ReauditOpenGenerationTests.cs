using System.Reflection;
using Ivi.Visa;
using VISA.Executors;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace VISA.Tests;

public sealed class ReauditOpenGenerationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_open_cannot_close_a_new_session_returned_across_sdk_cleanup(bool cleanupAll)
    {
        using var guard = new CancellationTokenSource(15000);
        var actual = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var registry = DispatchProxy.Create<IResourceRegistry, LookupBoundaryRegistry>();
        var boundary = (LookupBoundaryRegistry)(object)registry;
        boundary.Inner = actual;
        string key = VisaHelper.GetSessionKey("C");
        var native = DispatchProxy.Create<IMessageBasedSession, PublicationBoundaryTests.ControlledSession>();
        var fresh = new VisaSessionResource(native, 1000, "\n");
        bool replaced = false;
        boundary.BeforeSessionLookup = () =>
        {
            if (replaced) return;
            replaced = true;
            actual.GetType().GetMethod(cleanupAll ? "CleanupAll" : "CleanupExecution", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null);
            Assert.True(actual.TryAdd(key, fresh, ResourceLifetime.Execution));
        };
        var (context, state) = PublicationContextProxy.Create(new VisaOpenPlugin(), new VisaOpenSetting
        { ConnectionName = "\"C\"", ResourceString = "\"TCPIP::127.0.0.1::55556::SOCKET\"", OpenTimeoutMs = 1000 });
        state.ResourcesOverride = registry;
        int opens = 0;
        try
        {
            var result = await new VisaOpenExecutor((_, _) => { opens++; return native; }).ExecuteAsync(context, guard.Token);
            Assert.True(replaced);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, opens);
            Assert.True(actual.TryGet<VisaSessionResource>(key, out var current));
            Assert.Same(fresh, current);
            Assert.False(fresh.IsClosed);
            fresh.EnsureAvailableForPublication();
        }
        finally
        {
            await fresh.CloseAsync(2000);
            actual.GetType().GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null);
        }
    }
}

public class LookupBoundaryRegistry : DispatchProxy
{
    internal IResourceRegistry Inner = null!;
    internal Action? BeforeSessionLookup;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "TryGet" && method.IsGenericMethod && method.GetGenericArguments()[0] == typeof(VisaSessionResource))
            BeforeSessionLookup?.Invoke();
        try { return method.Invoke(Inner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
}
