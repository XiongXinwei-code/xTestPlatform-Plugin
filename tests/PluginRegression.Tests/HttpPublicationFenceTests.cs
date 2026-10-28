using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Http;
using Http.Executors;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class HttpPublicationFenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Slow_old_epoch_builder_or_close_does_not_hold_a_new_epochs_same_name_publication(bool holdBuilder)
    {
        using var guard = new CancellationTokenSource(15000);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actual = SdkResourceLifecycleTests.NewRegistry();
        var oldContext = Context(actual);
        var state = (SdkContext)(object)oldContext;
        HttpClientResource? oldOwner = null;
        Task<ExecutionResult> oldRun;
        if (holdBuilder)
        {
            state.SetStep(new HttpClientCreatePlugin(), new HttpClientCreateSetting { TimeoutMs = 10000 });
            var creator = new HttpClientCreateExecutor((build, timeout, caller, stop) => HttpHelper.BuildClientAsync(() =>
            { entered.TrySetResult(); release.Wait(guard.Token); return build(); }, timeout, caller, stop));
            oldRun = creator.ExecuteAsync(oldContext);
        }
        else
        {
            oldOwner = new HttpClientResource { Client = new HttpClient(new BlockingDisposeHandler(entered, release, guard.Token)) };
            actual.Set(HttpHelper.GetClientKey("Mes"), oldOwner);
            state.SetStep(new HttpClientClosePlugin(), new HttpClientCloseSetting { TimeoutMs = 10000 });
            oldRun = new HttpClientCloseExecutor().ExecuteAsync(oldContext);
        }
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            SdkResourceLifecycleTests.Cleanup(actual, "CleanupExecution");
            var nextContext = Context(actual);
            ((SdkContext)(object)nextContext).SetStep(new HttpClientCreatePlugin(), new HttpClientCreateSetting { TimeoutMs = 5000 });
            var result = await new HttpClientCreateExecutor().ExecuteAsync(nextContext).WaitAsync(guard.Token);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.False(release.IsSet);
            if (oldOwner != null) Assert.False(oldOwner.IsClosed);
            Assert.Equal(TestStatus.Error, (await oldRun.WaitAsync(guard.Token)).StepResult.Status);
            Assert.True(actual.TryGet<HttpClientResource>(HttpHelper.GetClientKey("Mes"), out var next));
            release.Set();
            if (oldOwner != null) await oldOwner.CloseAsync(2000);
            next.CheckPublication(actual, HttpHelper.GetClientKey("Mes"), default);
        }
        finally
        {
            release.Set(); await oldRun.WaitAsync(guard.Token);
            if (oldOwner != null) await oldOwner.CloseAsync(2000);
            SdkResourceLifecycleTests.Cleanup(actual, "CleanupAll");
        }
    }

    [Theory]
    [InlineData("Remove")]
    [InlineData("Set")]
    [InlineData("TryAdd")]
    public async Task Old_epoch_registry_mutation_cannot_remove_or_replace_a_new_clients_publication(string mutation)
    {
        using var guard = new CancellationTokenSource(20000);
        using var releaseMutation = new ManualResetEventSlim();
        var actual = SdkResourceLifecycleTests.NewRegistry();
        var registry = DispatchProxy.Create<IResourceRegistry, MutationBarrierRegistry>();
        var probe = (MutationBarrierRegistry)(object)registry;
        probe.Inner = actual; probe.Release = releaseMutation; probe.Guard = guard.Token;
        string key = HttpHelper.GetClientKey("Mes");
        var old = new HttpClientResource { Client = new HttpClient() };
        if (mutation == "Remove") actual.Set(key, old);
        var oldContext = Context(registry);
        var oldState = (SdkContext)(object)oldContext;
        if (mutation == "Remove") oldState.SetStep(new HttpClientClosePlugin(), new HttpClientCloseSetting { TimeoutMs = 15000 });
        else oldState.SetStep(new HttpClientCreatePlugin(), new HttpClientCreateSetting { TimeoutMs = 15000, ReplaceIfExists = mutation == "Set" });
        probe.BlockMethod = mutation;
        var oldRun = Task.Run(() => mutation == "Remove"
            ? new HttpClientCloseExecutor().ExecuteAsync(oldContext)
            : new HttpClientCreateExecutor().ExecuteAsync(oldContext));
        Task<ExecutionResult>? nextRun = null;
        try
        {
            await probe.Entered.Task.WaitAsync(guard.Token);
            SdkResourceLifecycleTests.Cleanup(actual, "CleanupExecution");
            var nextContext = Context(registry);
            ((SdkContext)(object)nextContext).SetStep(new HttpClientCreatePlugin(), new HttpClientCreateSetting { TimeoutMs = 15000 });
            var built = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var creator = new HttpClientCreateExecutor((build, timeout, caller, stop) => HttpHelper.BuildClientAsync(() =>
            {
                var client = build(); built.TrySetResult(); return client;
            }, timeout, caller, stop));
            nextRun = creator.ExecuteAsync(nextContext);
            await built.Task.WaitAsync(guard.Token);
            await Task.WhenAny(nextRun, Task.Delay(100, guard.Token));
            Assert.False(nextRun.IsCompleted, "A new epoch may build immediately but must wait for the in-flight short registry mutation fence");
            releaseMutation.Set();
            Assert.Equal(TestStatus.Error, (await oldRun.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(TestStatus.Completed, (await nextRun.WaitAsync(guard.Token)).StepResult.Status);
            Assert.True(actual.TryGet<HttpClientResource>(key, out var current));
            Assert.NotSame(old, current); Assert.False(current.IsClosed);
            current.CheckPublication(registry, key, default);
        }
        finally
        {
            releaseMutation.Set();
            await oldRun.WaitAsync(guard.Token);
            if (nextRun != null) await nextRun.WaitAsync(guard.Token);
            SdkResourceLifecycleTests.Cleanup(actual, "CleanupAll");
            await old.CloseAsync(2000);
        }
    }

    [Fact]
    public async Task Case_variants_share_the_same_lifecycle_name_as_the_real_sdk_registry()
    {
        using var guard = new CancellationTokenSource(15000);
        using var builderExit = new ManualResetEventSlim();
        var registry = SdkResourceLifecycleTests.NewRegistry();
        var createContext = Context(registry);
        ((SdkContext)(object)createContext).SetStep(new HttpClientCreatePlugin(), new HttpClientCreateSetting { ClientName = "\"MES\"", TimeoutMs = 10000 });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var creator = new HttpClientCreateExecutor((build, timeout, caller, stop) => HttpHelper.BuildClientAsync(() =>
        { entered.TrySetResult(); builderExit.Wait(guard.Token); return build(); }, timeout, caller, stop));
        var creating = creator.ExecuteAsync(createContext);
        Task<ExecutionResult>? closing = null;
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            var closeContext = Context(registry);
            ((SdkContext)(object)closeContext).SetStep(new HttpClientClosePlugin(), new HttpClientCloseSetting { ClientName = "\"mes\"", TimeoutMs = 10000 });
            closing = new HttpClientCloseExecutor().ExecuteAsync(closeContext);
            await Task.WhenAny(closing, Task.Delay(100, guard.Token));
            Assert.False(closing.IsCompleted);
            builderExit.Set();
            Assert.Equal(TestStatus.Completed, (await creating.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(TestStatus.Completed, (await closing.WaitAsync(guard.Token)).StepResult.Status);
            Assert.False(registry.Contains(HttpHelper.GetClientKey("Mes")));
        }
        finally
        {
            builderExit.Set(); await creating.WaitAsync(guard.Token);
            if (closing != null) await closing.WaitAsync(guard.Token);
            SdkResourceLifecycleTests.Cleanup(registry, "CleanupAll");
        }
    }

    private static IExecutionContext Context(IResourceRegistry registry)
    {
        var context = DispatchProxy.Create<IExecutionContext, SdkContext>();
        ((SdkContext)(object)context).Registry = registry;
        return context;
    }

    public class MutationBarrierRegistry : DispatchProxy
    {
        internal IResourceRegistry Inner = null!;
        internal ManualResetEventSlim Release = null!;
        internal CancellationToken Guard;
        internal string? BlockMethod;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == BlockMethod && Equals(args![0], HttpHelper.GetClientKey("Mes")) &&
                Interlocked.Exchange(ref BlockMethod, null) != null)
            { Entered.TrySetResult(); Release.Wait(Guard); }
            try { return method.Invoke(Inner, args); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        }
    }

    private sealed class BlockingDisposeHandler(TaskCompletionSource entered, ManualResetEventSlim release, CancellationToken guard) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        { if (disposing) { entered.TrySetResult(); release.Wait(guard); } base.Dispose(disposing); }
    }
}
