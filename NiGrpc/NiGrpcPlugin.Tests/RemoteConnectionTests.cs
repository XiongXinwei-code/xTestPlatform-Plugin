using NiGrpc.Runtime;
using Xunit;

namespace NiGrpc.Tests;

public sealed class RemoteConnectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Removed_open_slot_still_keeps_connection_until_its_callback_exits(bool rejected)
    {
        using var caller = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var wire = new Wire { BadInput = rejected, OnOpen = token =>
        {
            token.Register(() => { entered.Set(); release.Wait(); });
            caller.Cancel();
        } };
        var owner = new RemoteConnection(wire, wire, 1000);
        try
        {
            if (rejected)
                await Assert.ThrowsAsync<LocalInputException>(() => owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, caller.Token));
            else
            {
                await owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, caller.Token);
                await owner.CloseSessionAsync("axis", 1000, default);
            }
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
            var close = owner.RequestClose(1000);
            await Assert.ThrowsAnyAsync<TimeoutException>(() => close.WaitAsync(TimeSpan.FromMilliseconds(100)));
            Assert.False(owner.LocalChannelClosed);
            release.Set();
            await close.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(owner.LocalChannelClosed);
        }
        finally { release.Set(); }
    }
    [Fact]
    public async Task Timed_out_open_keeps_channel_until_actual_open_exits_and_stays_unknown()
    {
        var wire = new Wire { PendingOpen = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        string? unknown = null;
        var owner = new RemoteConnection(wire, wire, 50, unknown: reason => unknown = reason);
        await Assert.ThrowsAsync<TimeoutException>(() => owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 40, default));
        var close = owner.RequestClose(50);
        await Assert.ThrowsAnyAsync<TimeoutException>(() => close.WaitAsync(TimeSpan.FromMilliseconds(100)));
        Assert.False(owner.LocalChannelClosed);
        Assert.NotNull(unknown);
        wire.PendingOpen.SetResult(new OpenReply(wire.Name!, true, 0));
        await Assert.ThrowsAsync<AggregateException>(() => close.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(owner.LocalChannelClosed);
        Assert.Equal(0, wire.Clears);
    }
    [Fact]
    public async Task Exhausted_close_budget_does_not_relabel_known_unsubmitted_open_as_unknown()
    {
        var wire = new Wire { PendingClear = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var owner = new RemoteConnection(wire, wire, 1000);
        await owner.OpenSessionAsync("first", NiDriver.Daqmx, "{}", 1000, default);
        wire.PendingOpen = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = owner.OpenSessionAsync("second", NiDriver.Daqmx, "{}", 1000, default);
        var close = owner.RequestClose(100);
        wire.PendingOpen.SetException(new LocalInputException("configuration rejected before submit"));
        await Assert.ThrowsAsync<LocalInputException>(() => second);
        await Assert.ThrowsAnyAsync<TimeoutException>(() => close.WaitAsync(TimeSpan.FromMilliseconds(200)));
        Assert.False(owner.LocalChannelClosed);
        wire.PendingClear.SetResult();
        var error = await Assert.ThrowsAsync<AggregateException>(() => close);
        Assert.Contains("first", error.ToString());
        Assert.DoesNotContain("会话别名=second", error.ToString());
    }
    [Fact]
    public async Task Session_open_entry_with_cancelled_token_never_calls_remote()
    {
        var wire = new Wire();
        using var caller = new CancellationTokenSource(); caller.Cancel();
        await Assert.ThrowsAsync<NotSubmittedCanceledException>(() => RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, caller.Token));
        Assert.Null(wire.Name);
    }

    [Fact]
    public async Task Closing_while_unsubmitted_open_is_cancelled_does_not_report_unknown_remote()
    {
        var wire = new Wire();
        var owner = new RemoteConnection(wire, wire, 3000);
        using var caller = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var open = Task.Run(() => owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, caller.Token,
            () => { entered.Set(); release.Wait(); }));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
        var close = owner.RequestClose(2000);
        caller.Cancel(); release.Set();
        await Assert.ThrowsAsync<NotSubmittedCanceledException>(() => open);
        await close.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(owner.LocalChannelClosed);
        Assert.Null(wire.Name);
    }
    [Fact]
    public async Task Caller_cancelled_before_create_rpc_removes_alias()
    {
        var wire = new Wire();
        var owner = new RemoteConnection(wire, wire, 1000);
        using var caller = new CancellationTokenSource();
        await Assert.ThrowsAsync<NotSubmittedCanceledException>(() => owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, caller.Token, caller.Cancel));
        Assert.Null(wire.Name);
        await owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, default);
        await owner.CloseAsync(1000, default);
    }

    [Fact]
    public async Task Scope_retired_before_create_rpc_removes_alias()
    {
        var wire = new Wire();
        var owner = new RemoteConnection(wire, wire, 1000);
        await Assert.ThrowsAsync<LocalInputException>(() => owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, default, scopeStop: Task.CompletedTask));
        Assert.Null(wire.Name);
        await owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, default);
        await owner.CloseAsync(1000, default);
    }
    [Fact]
    public async Task Local_channel_failure_preserves_remote_clear_failure_and_registry_ownership()
    {
        var wire = new Wire { FailClear = true, FailDispose = true };
        var owner = new RemoteConnection(wire, wire, 1000);
        var session = await owner.OpenSessionAsync("deviceC", NiDriver.Daqmx, "{}", 1000, default);
        var error = await Assert.ThrowsAsync<AggregateException>(() => owner.RequestClose(1000));
        Assert.Contains("clear failed", error.ToString());
        Assert.Contains("dispose failed", error.ToString());
        Assert.Contains(session.RemoteName, error.ToString());
        Assert.Contains("本地 gRPC 通道关闭未完成", error.Message);
        Assert.DoesNotContain("通道已关闭", error.Message);
        Assert.False(owner.LocalChannelClosed);
    }
    [Fact]
    public async Task Pre_submit_rejection_does_not_reserve_alias_or_send_create()
    {
        var wire = new Wire();
        var owner = new RemoteConnection(wire, wire, 1000);
        await Assert.ThrowsAsync<LocalInputException>(() => owner.OpenSessionAsync("deviceA", NiDriver.Daqmx, "{}", 1000, default,
            () => throw new InvalidOperationException("epoch retired")));
        Assert.Null(wire.Name);
        var session = await owner.OpenSessionAsync("deviceA", NiDriver.Daqmx, "{}", 1000, default);
        Assert.Equal(RemoteState.Active, session.State);
        await owner.CloseAsync(1000, default);
    }

    [Fact]
    public async Task Explicit_close_timeout_eventually_reports_actual_session_failure()
    {
        var wire = new Wire { PendingClear = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new RemoteConnection(wire, wire, 40, e => reported.TrySetResult(e));
        var session = await owner.OpenSessionAsync("deviceB", NiDriver.Daqmx, "{}", 1000, default);
        await Assert.ThrowsAnyAsync<Exception>(() => owner.CloseAsync(40, default));
        await Task.Delay(100);
        Assert.False(owner.LocalChannelClosed);
        wire.PendingClear.SetResult();
        var error = await reported.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Contains("deviceB", error.ToString());
        Assert.Contains(session.RemoteName, error.ToString());
    }
    [Fact]
    public async Task Automatic_dispose_reports_actual_close_failure_with_remote_identity()
    {
        var wire = new Wire { PendingClear = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new RemoteConnection(wire, wire, 40, e => reported.TrySetResult(e));
        var session = await owner.OpenSessionAsync("deviceA", NiDriver.Daqmx, "{}", 1000, default);
        owner.Dispose();
        await Task.Delay(120);
        Assert.False(owner.LocalChannelClosed);
        wire.PendingClear.SetResult();
        var error = await reported.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Contains("deviceA", error.ToString());
        Assert.Contains(session.RemoteName, error.ToString());
    }
    [Fact]
    public async Task Close_budget_identifies_each_session_not_proven_released()
    {
        var wire = new Wire { PendingClear = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var owner = new RemoteConnection(wire, wire, 1000);
        var first = await owner.OpenSessionAsync("first", NiDriver.Daqmx, "{}", 1000, default);
        var second = await owner.OpenSessionAsync("second", NiDriver.Daqmx, "{}", 1000, default);
        var close = owner.RequestClose(50);
        await Assert.ThrowsAnyAsync<TimeoutException>(() => close.WaitAsync(TimeSpan.FromMilliseconds(200)));
        Assert.False(owner.LocalChannelClosed);
        wire.PendingClear.SetResult();
        var error = await Assert.ThrowsAsync<AggregateException>(() => close);
        var detail = error.ToString();
        Assert.Contains("first", detail); Assert.Contains(first.RemoteName, detail);
        Assert.Contains("second", detail); Assert.Contains(second.RemoteName, detail);
        Assert.Equal(2, wire.Clears);
    }
    [Fact]
    public async Task Unsubmitted_bad_configuration_does_not_reserve_alias()
    {
        var wire = new Wire { BadInput = true };
        var owner = new RemoteConnection(wire, wire, 2000);
        await Assert.ThrowsAsync<LocalInputException>(() => owner.OpenSessionAsync("axis", NiDriver.Xnet, "{}", 1000, default));
        wire.BadInput = false;
        var session = await owner.OpenSessionAsync("axis", NiDriver.Xnet, "{}", 1000, default);
        Assert.Equal(RemoteState.Active, session.State);
        await owner.CloseAsync(2000, default);
        Assert.Equal(1, wire.Clears);
    }

    [Fact]
    public async Task Closing_pending_creation_uses_step_budget_without_sending_clear()
    {
        var wire = new Wire { PendingOpen = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var owner = new RemoteConnection(wire, wire, 2000);
        var opening = owner.OpenSessionAsync("axis", NiDriver.Xnet, "{}", 1000, default);
        await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseSessionAsync("axis", 30, default));
        Assert.Equal(0, wire.Clears);
        wire.PendingOpen.SetResult(new OpenReply(wire.Name!, true, 0));
        await opening;
        await owner.CloseAsync(2000, default);
        Assert.Equal(1, wire.Clears);
    }

    [Fact]
    public async Task Opening_completed_during_disconnect_cannot_report_usable_session()
    {
        var wire = new Wire { PendingOpen = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var owner = new RemoteConnection(wire, wire, 2000);
        var opening = owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, default);
        var closing = owner.CloseAsync(2000, default);
        wire.PendingOpen.SetResult(new OpenReply(wire.Name!, true, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => opening);
        await closing;
        Assert.Equal(1, wire.Clears);
        Assert.True(owner.LocalChannelClosed);
    }

    [Fact]
    public async Task Concurrent_close_calls_submit_one_clear()
    {
        var wire = new Wire();
        var owner = new RemoteConnection(wire, wire, 2000);
        var session = await owner.OpenSessionAsync("axis", NiDriver.Daqmx, "{}", 1000, default);
        await Task.WhenAll(session.CloseAsync(1000, default), session.CloseAsync(1000, default));
        Assert.Equal(1, wire.Clears);
        await owner.CloseAsync(2000, default);
        Assert.Equal(1, wire.Clears);
    }

    private sealed class Wire : IRemoteWire, IDisposable
    {
        internal bool BadInput;
        internal bool FailClear, FailDispose;
        internal int Clears;
        internal string? Name;
        internal TaskCompletionSource<OpenReply>? PendingOpen;
        internal TaskCompletionSource? PendingClear;
        internal Action<CancellationToken>? OnOpen;
        public Task<OpenReply> OpenAsync(NiDriver driver, string name, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null)
        {
            Name = name;
            OnOpen?.Invoke(ct);
            if (BadInput) return Task.FromException<OpenReply>(new LocalInputException("missing mode"));
            return PendingOpen?.Task ?? Task.FromResult(new OpenReply(name, true, 0));
        }
        public Task<string> InvokeAsync(NiDriver driver, string name, string method, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null) => Task.FromResult("{}");
        public Task ClearAsync(NiDriver driver, string name, int timeoutMs, CancellationToken ct) { Interlocked.Increment(ref Clears); return FailClear ? Task.FromException(new InvalidOperationException("clear failed")) : PendingClear?.Task ?? Task.CompletedTask; }
        public void Dispose() { if (FailDispose) throw new InvalidOperationException("dispose failed"); }
    }
}
