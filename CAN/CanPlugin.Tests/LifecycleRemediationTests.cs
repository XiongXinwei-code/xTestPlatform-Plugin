using CAN.Runtime;
using CAN.UDS;
using CAN.XCP;
using CAN.Adapters;
using CAN.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class LifecycleRemediationTests
{
    [Fact]
    public async Task XcpUploadRejectsTruncatedPositiveResponse()
    {
        var raw = new FakeAdapter();
        raw.OnWrite = _ => raw.Incoming.Add(new() { Id = 0x581, Data = [0xFF, 0x12] });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new XcpClient(raw, 0x601, 0x581, 1000).ShortUploadAsync(0x1000, 0, 7));
    }

    [Fact]
    public async Task OpenExecutorCanReplaceReleasedHistoricalOwnerOnItsFirstAttempt()
    {
        var context = System.Reflection.DispatchProxy.Create<xTestPlatform.Core.Engine.IExecutionContext, CloseContext>();
        var state = (CloseContext)(object)context;
        var raw = new ReleasedWithHistory(); raw.Release.Set();
        var old = new CanConnectionResource(raw);
        await Assert.ThrowsAsync<InvalidOperationException>(() => old.CloseAsync());
        Assert.True(old.IsClosed);
        state.Adapter = old; state.Plugin = new CanOpenPlugin();
        state.Setting = new CanOpenSetting { AdapterType = CanAdapterType.Kvaser, Channel = "\"0\"", ConnectionName = "\"CAN1\"" };
        var next = new FakeAdapter();
        try
        {
            var result = await new CAN.Executors.CanOpenExecutor(_ => next).ExecuteAsync(context);
            Assert.Equal(xTestPlatform.Core.Models.TestStatus.Completed, result.StepResult.Status);
            Assert.NotSame(old, state.Adapter); Assert.True(state.Adapter!.IsConnected);
        }
        finally { if (state.Adapter is CanConnectionResource owner && !ReferenceEquals(owner, old)) await owner.CloseAsync(); }
    }

    [Fact]
    public async Task FirstReopenCanProceedWhenOldCloseReleasedHandlesThenReportsHistory()
    {
        string key = Guid.NewGuid().ToString(); var raw = new ReleasedWithHistory();
        var reports = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var owner = new CanConnectionResource(raw, key, reports.Enqueue); owner.Dispose();
        await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var reopen = CanConnectionResource.WaitForPreviousOwnerAsync(key, 2000);
        raw.Release.Set();
        await reopen;
        Assert.True(owner.IsClosed); Assert.True(raw.HandlesReleased);
        var replacement = new CanConnectionResource(new FakeAdapter(), key);
        await replacement.CloseAsync();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (reports.IsEmpty && clock.ElapsedMilliseconds < 1000) await Task.Delay(5);
        Assert.Contains(reports, e => e.ToString().Contains("历史故障"));
    }

    private sealed class ReleasedWithHistory : ICanAdapter, ICanCloseState
    {
        internal readonly ManualResetEventSlim Release = new();
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsConnected => !HandlesReleased;
        public bool HandlesReleased { get; private set; }
        public void Open(CanAdapterConfig config) { }
        public void Write(CanMessage message) { }
        public CanMessage? Read(int timeoutMs, CancellationToken ct = default) => null;
        public CanMessage? Read(uint id, int timeoutMs, CancellationToken ct = default) => null;
        public void Close() { Entered.TrySetResult(); Release.Wait(); HandlesReleased = true; throw new InvalidOperationException("已释放句柄，报告历史故障"); }
        public void Dispose() => Close();
    }

    [Fact]
    public async Task CompositeXcpDownloadOwnsSetMtaAndDownloadAcrossClientInstances()
    {
        var raw = new FakeAdapter(); var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int mta = 0;
        raw.OnWrite = frame =>
        {
            if (frame.Data[0] == 0xF6 && Interlocked.Increment(ref mta) == 1) first.TrySetResult();
            else raw.Incoming.Add(new() { Id = 0x581, Data = [0xFF] });
        };
        var a = new XcpClient(raw, 0x601, 0x581, 2000).ShortDownloadAsync(0x1000, 0, [1]);
        await first.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var b = new XcpClient(raw, 0x601, 0x581, 2000).ShortDownloadAsync(0x2000, 0, [2]);
        try { await Task.Delay(40); Assert.Single(raw.Sent); }
        finally { raw.Incoming.Add(new() { Id = 0x581, Data = [0xFF] }); }
        await Task.WhenAll(a, b);
        Assert.Equal(new byte[] { 0xF6, 0xF0, 0xF6, 0xF0 }, raw.Sent.Select(f => f.Data[0]));
    }

    [Fact]
    public async Task UdsPendingResponsesAreLimitedByWholeTransactionDeadline()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        raw.OnWrite = _ => { for (int i = 0; i < 100; i++) raw.Incoming.Add(new() { Id = 0x70C, Data = [3, 0x7F, 0x10, 0x78] }); };
        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            await Assert.ThrowsAsync<TimeoutException>(() => new UdsClient(owner, 0x704, 0x70C,
                responseTimeoutMs: 5000, transactionTimeoutMs: 200).RequestAsync([0x10, 1]));
            Assert.True(clock.ElapsedMilliseconds < 1500);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task DifferentProtocolQueueTimeoutNeverSubmitsARequest()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); using var stop = new CancellationTokenSource();
        var first = new XcpClient(owner, 0x601, 0x581, 2000).ConnectAsync(ct: stop.Token);
        await raw.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => new UdsClient(owner, 0x704, 0x70C,
                transactionTimeoutMs: 80).RequestAsync([0x10, 1]));
            Assert.Single(raw.Sent); Assert.Equal(0x601u, raw.Sent.Single().Id);
        }
        finally { stop.Cancel(); try { await first; } catch (OperationCanceledException) { } await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(-1)]
    public async Task RawReadCancelledWaitDoesNotReleasePendingNativeCall(int timeoutMs)
    {
        using var release = new ManualResetEventSlim(); using var stop = new CancellationTokenSource();
        var raw = new FakeAdapter { ReadBlock = release }; var owner = new CanConnectionResource(raw);
        var read = owner.ReadAsync(0x181, timeoutMs, stop.Token);
        try
        {
            await raw.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)); stop.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(TimeSpan.FromSeconds(1)));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.CloseCount);
        }
        finally { release.Set(); await owner.CloseAsync(); }
    }

    [Fact]
    public async Task CancelledXcpNeverSubmitsSetMta()
    {
        var raw = new FakeAdapter(); using var ct = new CancellationTokenSource(); ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new XcpClient(raw, 0x601, 0x581).ShortDownloadAsync(0x1000, 0, [1], ct.Token));
        Assert.Empty(raw.Sent);
    }

    [Fact]
    public async Task UdsCancelledBlockedWriteReturnsButKeepsActualOwner()
    {
        using var release = new ManualResetEventSlim(); using var stop = new CancellationTokenSource();
        var raw = new FakeAdapter { WriteBlock = release }; var owner = new CanConnectionResource(raw);
        Task<UdsResponse>? request = null; bool returned = false;
        var caller = Task.Run(async () =>
        {
            request = new UdsClient(owner, 0x704, 0x70C).RequestAsync([0x10, 1], stop.Token);
            returned = true; return await request;
        });
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)); stop.Cancel();
            await Task.Delay(150);
            Assert.True(returned, "异步入口必须返回Task，不能同步阻塞在驱动Write");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => caller.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.CloseCount);
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
        }
        finally { release.Set(); try { await caller; } catch (OperationCanceledException) { } await owner.CloseAsync(); }
    }

    [Fact]
    public async Task FaultedConfirmedMotionCanReleaseServiceWithoutHidingError()
    {
        var raw = new ConfirmedAdapter(); var owner = new CanConnectionResource(raw);
        // Monitoring no longer faults solely for a slow, confirmed batch.
        // Motion retains the strict period guard and provides a confirmed fault.
        raw.OnWrite = _ => Thread.Sleep(40);
        var service = owner.StartService("monitor", [new("feedback", 0x181, 1, 100)]);
        service.StartTargets([1], [new(0x201, [1])], new(0x80, []), new(10, 1000, 1000));
        try
        {
            var wait = System.Diagnostics.Stopwatch.StartNew();
            while (!service.Snapshot().Cycle.HostExited)
            { Assert.True(wait.ElapsedMilliseconds < 3000); await Task.Delay(5); }
            Assert.NotNull(service.Snapshot().Cycle.Error);
            try { await owner.StopServiceAsync("monitor", 1000, default); } catch (InvalidOperationException) { }
            Assert.Throws<InvalidOperationException>(() => owner.GetService("monitor"));
            Assert.NotNull(service.Snapshot().Cycle.Error);
        }
        finally { try { await owner.CloseAsync(); } catch (InvalidOperationException) when (owner.IsClosed) { } }
    }
}
