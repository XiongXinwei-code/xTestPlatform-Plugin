using System.Reflection;
using LIN.Adapters;
using LIN.Executors;
using LIN.Helpers;
using LIN.Models;
using LIN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace LIN.Tests;

[Collection("LIN managed retirement")]
public sealed class KeyedJobRetirementTests
{
    [Fact]
    public async Task Closing_A_cannot_remove_the_same_named_job_replaced_by_B_during_lookup()
    {
        var actual = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var registry = DispatchProxy.Create<IResourceRegistry, JobLookupBarrierRegistry>();
        var barrier = (JobLookupBarrierRegistry)(object)registry; barrier.Inner = actual;
        string taskKey = LinCyclicSendStartExecutor.GetTaskKey("shared"); barrier.Key = taskKey;
        var a = new LinConnectionResource(new Driver()); var b = new LinConnectionResource(new Driver());
        var oldJob = a.StartJob([_ => Task.CompletedTask]); oldJob.RegistrationKey = taskKey;
        await oldJob.Completion;
        actual.Set(LinHelper.GetAdapterKey("A"), a); actual.Set(LinHelper.GetAdapterKey("B"), b); actual.Set(taskKey, oldJob);
        var context = DispatchProxy.Create<IExecutionContext, SdkLinContext>();
        var state = (SdkLinContext)(object)context; state.Registry = registry; state.Plugin = new LinClosePlugin();
        state.Setting = new LinCloseSetting { ConnectionName = "\"A\"", TimeoutMs = 5000 };
        LinCyclicJob? replacement = null;
        var closing = Task.Run(() => new LinCloseExecutor().ExecuteAsync(context));
        async Task ReplaceAtTheCyclicStartRegistrationBoundary()
        {
            // Execute the same critical task-key transaction as CyclicStart. This
            // direct async call enters synchronously when unprotected, so RED
            // deterministically installs B before A resumes its cached lookup.
            using var lease = await LinRegistryGuard.Enter(registry, default, 5000, taskKey);
            if (registry.TryGet<LinCyclicJob>(taskKey, out var old)) { await old.StopAsync(); registry.Remove(taskKey); }
            replacement = b.StartJob([token => Task.Delay(Timeout.Infinite, token)]);
            replacement.RegistrationKey = taskKey;
            Assert.True(registry.TryAdd(taskKey, replacement));
        }
        try
        {
            await barrier.LookupEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var replacing = ReplaceAtTheCyclicStartRegistrationBoundary();
            barrier.LookupExit.TrySetResult();
            Assert.Equal(TestStatus.Completed, (await closing).StepResult.Status);
            await replacing;
            Assert.NotNull(replacement);
            Assert.True(actual.TryGet<LinCyclicJob>(taskKey, out var registered));
            Assert.Same(replacement, registered);
            Assert.False(replacement.Completion.IsCompleted);
        }
        finally
        {
            barrier.LookupExit.TrySetResult(); await closing;
            await a.CloseAsync(); await b.CloseAsync();
        }
    }

    private sealed class Driver : ILinAdapter
    {
        public bool IsConnected => true;
        public void Open(LinAdapterConfig config) { }
        public void Close() { }
        public void Dispose() { }
        public void Write(LinFrame frame) { }
        public LinFrame? Read(int timeoutMs, CancellationToken ct = default) => null;
        public LinFrame? Read(byte id, int timeoutMs, CancellationToken ct = default) => null;
        public void Wakeup(bool remote = true) { }
        public void Sleep(bool remote = true) { }
    }
}

public class JobLookupBarrierRegistry : DispatchProxy
{
    internal IResourceRegistry Inner = null!;
    internal string Key = "";
    internal readonly TaskCompletionSource LookupEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource LookupExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _intercepted;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var result = method!.Invoke(Inner, args);
        if (method.Name == "TryGet" && method.GetGenericArguments()[0] == typeof(LinCyclicJob) &&
            Equals(args?[0], Key) && Interlocked.Exchange(ref _intercepted, 1) == 0)
        {
            LookupEntered.TrySetResult();
            LookupExit.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        }
        return result;
    }
}
