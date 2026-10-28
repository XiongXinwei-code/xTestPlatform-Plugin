using System.Reflection;
using System.Runtime.ExceptionServices;
using Http;
using Http.Helpers;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class FollowupHttpLookupTests
{
    [Theory]
    [InlineData(false, "cancel")]
    [InlineData(true, "cancel")]
    [InlineData(false, "CleanupExecution")]
    [InlineData(true, "CleanupExecution")]
    [InlineData(false, "CleanupAll")]
    [InlineData(true, "CleanupAll")]
    [InlineData(false, "missing")]
    [InlineData(true, "missing")]
    public async Task Missing_client_lookup_preserves_the_original_cancellation_and_execution_scope(bool soap, string boundary)
    {
        IStepPlugin plugin = soap ? new HttpSoapRequestPlugin() : new HttpRequestPlugin();
        var actual = SdkResourceLifecycleTests.NewRegistry();
        var resources = DispatchProxy.Create<IResourceRegistry, FollowupHttpLookupRegistry>();
        var probe = (FollowupHttpLookupRegistry)(object)resources; probe.Inner = actual;
        var context = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var state = (SdkContext)(object)context; state.Registry = resources;
        state.SetStep(plugin, plugin.CreateSerializer().CreateDefault());
        using var caller = new CancellationTokenSource();
        probe.BeforeClientLookup = () =>
        {
            if (boundary == "cancel") caller.Cancel();
            else if (boundary != "missing") SdkResourceLifecycleTests.Cleanup(actual, boundary);
        };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.True(probe.Entered);
            Assert.Equal(boundary == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            if (boundary.StartsWith("Cleanup", StringComparison.Ordinal))
                Assert.Contains("作用域", result.StepResult.Error!.Message + result.StepResult.Error.Detail);
            Assert.Empty(state.Variables); Assert.False(actual.Contains(HttpHelper.GetClientKey("Mes")));

            var rerun = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, rerun.StepResult.Status);
            Assert.Contains("未找到 HTTP 客户端", rerun.StepResult.Error!.Message);
            Assert.Empty(state.Variables);
        }
        finally { SdkResourceLifecycleTests.Cleanup(actual, "CleanupAll"); }
    }
}

public class FollowupHttpLookupRegistry : DispatchProxy
{
    internal IResourceRegistry Inner = null!;
    internal Action? BeforeClientLookup;
    internal bool Entered;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "TryGet" && method.GetGenericArguments()[0] == typeof(HttpClientResource))
        {
            Entered = true;
            Interlocked.Exchange(ref BeforeClientLookup, null)?.Invoke();
        }
        try { return method.Invoke(Inner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
}
