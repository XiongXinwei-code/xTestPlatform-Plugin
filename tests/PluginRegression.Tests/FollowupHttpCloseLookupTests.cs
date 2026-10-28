using System.Reflection;
using System.Runtime.ExceptionServices;
using Http;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class FollowupHttpCloseLookupTests
{
    [Theory]
    [InlineData(false, "cancel")] [InlineData(true, "cancel")]
    [InlineData(false, "CleanupExecution")] [InlineData(true, "CleanupExecution")]
    [InlineData(false, "CleanupAll")] [InlineData(true, "CleanupAll")]
    [InlineData(false, "missing")] [InlineData(true, "missing")]
    public async Task Close_missing_branches_cannot_remove_a_new_generation_or_hide_stop(bool afterContains, string boundary)
    {
        var actual = SdkResourceLifecycleTests.NewRegistry();
        var registry = DispatchProxy.Create<IResourceRegistry, CloseLookupRegistry>();
        var probe = (CloseLookupRegistry)(object)registry; probe.Inner = actual; probe.LookupBranch = afterContains;
        var context = DispatchProxy.Create<IExecutionContext, SdkContext>(); var state = (SdkContext)(object)context; state.Registry = registry;
        var plugin = new HttpClientClosePlugin(); state.SetStep(plugin, new HttpClientCloseSetting { IgnoreIfNotFound = false });
        using var caller = new CancellationTokenSource();
        var fresh = new HttpClientResource { Client = new System.Net.Http.HttpClient() };
        string key = HttpHelper.GetClientKey("Mes");
        probe.AtBoundary = () =>
        {
            if (boundary == "cancel") caller.Cancel();
            else if (boundary != "missing") SdkResourceLifecycleTests.Cleanup(actual, boundary);
            // The old failed lookup returned false. A fresh run may register before the old continuation.
            if (afterContains && boundary != "missing") Assert.True(actual.TryAdd(key, fresh));
        };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(probe.Entered);
            Assert.Equal(boundary == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, probe.Removes);
            if (boundary.StartsWith("Cleanup", StringComparison.Ordinal))
                Assert.Contains("作用域", result.StepResult.Error!.Message + result.StepResult.Error.Detail);
            if (afterContains && boundary != "missing")
            { Assert.True(actual.TryGet<HttpClientResource>(key, out var current)); Assert.Same(fresh, current); Assert.False(fresh.IsClosed); }
        }
        finally { SdkResourceLifecycleTests.Cleanup(actual, "CleanupAll"); await fresh.CloseAsync(2000); }
    }

    public class CloseLookupRegistry : DispatchProxy
    {
        internal IResourceRegistry Inner = null!;
        internal Action? AtBoundary;
        internal bool LookupBranch, Entered;
        internal int Removes;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            string key = HttpHelper.GetClientKey("Mes");
            if (method!.Name == "Contains" && Equals(args![0], key))
            {
                if (LookupBranch) return true;
                Entered = true; Interlocked.Exchange(ref AtBoundary, null)?.Invoke(); return false;
            }
            if (method.Name == "TryGet" && method.GetGenericArguments()[0] == typeof(HttpClientResource))
            {
                Entered = true; args![1] = null;
                Interlocked.Exchange(ref AtBoundary, null)?.Invoke(); return false;
            }
            if (method.Name == "Remove" && Equals(args![0], key)) Removes++;
            try { return method.Invoke(Inner, args); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        }
    }
}
