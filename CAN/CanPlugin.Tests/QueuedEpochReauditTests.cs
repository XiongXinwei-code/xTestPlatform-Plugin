using System.Reflection;
using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using CAN.UDS;
using xTestPlatform.Core.Engine;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class QueuedEpochReauditTests
{
    [Fact]
    public async Task SdkCleanupWhileRawDisposeIsPendingPreventsQueuedProtocolSubmission()
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>(); ((SdkCanContext)(object)context).Registry = registry;
        using var releaseWorker = new ManualResetEventSlim();
        using var releaseDispose = new ManualResetEventSlim();
        var workerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var raw = new DelayedDisposeAdapter(releaseDispose);
        registry.Set(CanHelper.GetAdapterKey("CAN1"), raw, ResourceLifetime.Execution);
        var guard = new CanExecutionGuard(context, default); guard.Watch(CanHelper.GetAdapterKey("CAN1"), raw);
        int held = 0;
        var flow = new AsyncLocal<bool>(change =>
        {
            if (change.ThreadContextChanged && change.CurrentValue && Interlocked.CompareExchange(ref held, 1, 0) == 0)
            { workerEntered.TrySetResult(); releaseWorker.Wait(5000); }
        });
        Task operation;
        flow.Value = true;
        try { operation = guard.RunAsync(() => new UdsClient(raw, 0x704, 0x70C).SendOnlyAsync([0x3E, 0x80])); }
        finally { flow.Value = false; }
        Task? cleanup = null;
        try
        {
            await workerEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cleanup = Task.Run(() => registry.GetType().GetMethod("CleanupExecution", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null));
            await raw.DisposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Throws<InvalidOperationException>(guard.CheckLifetime);
            releaseWorker.Set();
            await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
            Assert.Equal(0, raw.WriteCount);
            // The action-only epoch must not leak back into a later library consumer.
            var independent = new FakeAdapter();
            await new UdsClient(independent, 0x704, 0x70C).SendOnlyAsync([0x3E, 0x80]);
            Assert.Single(independent.Sent);
        }
        finally
        {
            releaseWorker.Set(); releaseDispose.Set();
            try { await operation; } catch { }
            if (cleanup != null) await cleanup;
        }
    }

    private sealed class DelayedDisposeAdapter(ManualResetEventSlim release) : ICanAdapter
    {
        public int WriteCount; public bool IsConnected => true;
        public readonly TaskCompletionSource DisposeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Open(CanAdapterConfig config) { } public void Close() { }
        public void Dispose() { DisposeEntered.TrySetResult(); if (!release.Wait(5000)) throw new TimeoutException("fixture disposal barrier"); }
        public void Write(CanMessage message) => Interlocked.Increment(ref WriteCount);
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => null;
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => null;
    }
}
