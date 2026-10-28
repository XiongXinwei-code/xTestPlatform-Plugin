using System.Reflection;
using System.Runtime.ExceptionServices;
using Http;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class FollowupHttpPublicationLookupTests
{
    [Theory]
    [InlineData("Globals.value")]
    [InlineData("CustomScope.value")]
    public void Declared_scope_is_resolved_by_context_and_not_a_plugin_whitelist(string path)
    {
        var (context, state) = TestContext.Create(new HttpJsonExtractPlugin(), new HttpJsonExtractSetting());
        state.DeclaredVariables[path] = "";
        Assert.Null(Record.Exception(() => HttpOutputPreflight.Validate(context, path, typeof(string), () => { }, optional: false)));
        state.DeclaredVariables.Clear();
        Assert.Throws<ArgumentException>(() => HttpOutputPreflight.Validate(context, path, typeof(string), () => { }, optional: false));
        Assert.Throws<ArgumentException>(() => HttpOutputPreflight.Validate(context, "value", typeof(string), () => { }, optional: false));
    }

    [Theory]
    [InlineData(false, "cancel")] [InlineData(true, "cancel")]
    [InlineData(false, "CleanupExecution")] [InlineData(true, "CleanupExecution")]
    [InlineData(false, "CleanupAll")] [InlineData(true, "CleanupAll")]
    [InlineData(false, "missing")] [InlineData(true, "missing")]
    public async Task Publication_lookup_rechecks_original_caller_and_sdk_epoch_before_owner_error(bool otherOwner, string boundary)
    {
        var actual = SdkResourceLifecycleTests.NewRegistry();
        var resources = DispatchProxy.Create<IResourceRegistry, PublicationLookupRegistry>();
        var probe = (PublicationLookupRegistry)(object)resources; probe.Inner = actual;
        var context = DispatchProxy.Create<IExecutionContext, SdkContext>(); var state = (SdkContext)(object)context; state.Registry = resources;
        var create = new HttpClientCreatePlugin(); state.SetStep(create, new HttpClientCreateSetting());
        string key = HttpHelper.GetClientKey("Mes");
        using var caller = new CancellationTokenSource();
        HttpClientResource? owner = null;
        var other = new HttpClientResource { Client = new System.Net.Http.HttpClient() };
        try
        {
            Assert.Equal(TestStatus.Completed, (await create.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Assert.True(actual.TryGet<HttpClientResource>(key, out owner));
            probe.Other = otherOwner ? other : null;
            probe.AtLookup = () =>
            {
                if (boundary == "cancel") caller.Cancel();
                else if (boundary != "missing") SdkResourceLifecycleTests.Cleanup(actual, boundary);
            };
            var error = Record.Exception(() => owner.CheckPublication(resources, key, caller.Token));
            Assert.True(probe.Entered);
            if (boundary == "cancel") Assert.IsAssignableFrom<OperationCanceledException>(error);
            else if (boundary != "missing") Assert.Contains("作用域", Assert.IsType<InvalidOperationException>(error).Message);
            else Assert.Contains("代次", Assert.IsType<InvalidOperationException>(error).Message);
            Assert.False(other.IsClosed);
        }
        finally { SdkResourceLifecycleTests.Cleanup(actual, "CleanupAll"); if (owner != null) await owner.CloseAsync(2000); await other.CloseAsync(2000); }
    }

    public class PublicationLookupRegistry : DispatchProxy
    {
        internal IResourceRegistry Inner = null!;
        internal Action? AtLookup;
        internal HttpClientResource? Other;
        internal bool Entered;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "TryGet" && method.GetGenericArguments()[0] == typeof(HttpClientResource) && AtLookup != null)
            {
                Entered = true; Interlocked.Exchange(ref AtLookup, null)?.Invoke();
                args![1] = Other; return Other != null;
            }
            try { return method.Invoke(Inner, args); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        }
    }
}
