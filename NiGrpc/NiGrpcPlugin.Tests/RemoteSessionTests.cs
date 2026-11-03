using NiGrpc.Runtime;
using Xunit;

namespace NiGrpc.Tests;

public sealed class RemoteSessionTests
{
    [Fact]
    public async Task Scope_cancel_after_clear_submission_reports_unknown_with_remote_name()
    {
        var wire = new FakeWire { PendingClear = new(TaskCreationOptions.RunContinuationsAsynchronously), CancelClearOnToken = true };
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var close = session.CloseAsync(1000, default, scopeStop: stop.Task);
        stop.SetResult();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => close);
        Assert.Contains("远端状态未知", error.Message);
        Assert.Contains(session.RemoteName, error.Message);
        Assert.Equal(RemoteState.Unknown, session.State);
        Assert.Equal(1, wire.Clears);
    }
    [Fact]
    public async Task Already_submitted_clear_with_unknown_result_is_not_retried()
    {
        var wire = new FakeWire { PendingClear = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var close = session.CloseAsync(50, default, scopeStop: stop.Task);
        stop.SetResult();
        await Assert.ThrowsAsync<TimeoutException>(() => close);
        Assert.Equal(RemoteState.Unknown, session.State);
        Assert.Equal(1, wire.Clears);
        wire.PendingClear.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CloseAsync(100, default));
        Assert.Equal(1, wire.Clears);
    }
    [Fact]
    public async Task Retired_queued_explicit_close_does_not_submit_clear()
    {
        var wire = new FakeWire { PendingCall = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        var first = session.InvokeAsync("StartTask", "{}", 1000, default);
        bool retired = false;
        var close = session.CloseAsync(1000, default, () => { if (retired) throw new InvalidOperationException("epoch retired"); });
        retired = true;
        wire.PendingCall.SetResult("{}");
        await first;
        await Assert.ThrowsAsync<LocalInputException>(() => close);
        Assert.Equal(0, wire.Clears);
        Assert.Equal(RemoteState.Active, session.State);
        await session.CloseAsync(1000, default);
        Assert.Equal(1, wire.Clears);
    }
    [Fact]
    public async Task Successful_clear_does_not_report_local_completion_until_callbacks_exit()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var caller = new CancellationTokenSource();
        var wire = new FakeWire { ClearCancellationCallback = () => { entered.Set(); release.Wait(); }, OnClear = caller.Cancel };
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        try
        {
            var close = session.CloseAsync(1000, caller.Token);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => close);
            Assert.NotEqual(RemoteState.Closed, session.State);
            Assert.Equal(1, wire.Clears);
        }
        finally { release.Set(); }
        await session.CloseAsync(1000, default);
        Assert.Equal(RemoteState.Closed, session.State);
        Assert.Equal(1, wire.Clears);
    }
    [Fact]
    public async Task Scope_already_retired_does_not_submit_or_isolate()
    {
        var wire = new FakeWire();
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        await Assert.ThrowsAsync<LocalInputException>(() => session.InvokeAsync("StartTask", "{}", 1000, default, scopeStop: Task.CompletedTask));
        Assert.Equal(0, wire.Calls);
        Assert.Equal(RemoteState.Active, session.State);
        await session.CloseAsync(1000, default);
    }
    [Fact]
    public async Task Caller_cancellation_is_forwarded_to_transport_before_caller_returns()
    {
        var wire = new FakeWire { PendingCall = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Xnet, "{}", 1000, default);
        using var stop = new CancellationTokenSource();
        var pending = session.InvokeAsync("WriteFrame", "{}", 1000, stop.Token);
        stop.Cancel();
        Assert.True(wire.OutboundToken.IsCancellationRequested);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        wire.PendingCall.SetResult("{}");
    }

    [Fact]
    public async Task Caller_cancellation_does_not_run_transport_callbacks_on_caller_thread()
    {
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        var wire = new FakeWire { PendingCall = new(TaskCreationOptions.RunContinuationsAsynchronously), CancellationCallback = () => { callbackEntered.Set(); releaseCallback.Wait(); } };
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Xnet, "{}", 1000, default);
        using var stop = new CancellationTokenSource();
        var pending = session.InvokeAsync("WriteFrame", "{}", 1000, stop.Token);
        await Task.Run(stop.Cancel).WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(callbackEntered.Wait(TimeSpan.FromSeconds(2)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        wire.PendingCall.SetResult("{}");
        await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync(50, default));
        Assert.Equal(0, wire.Clears);
        releaseCallback.Set();
    }
    [Fact]
    public async Task Confirmed_unsubmitted_timeout_does_not_isolate_session()
    {
        var wire = new FakeWire { NotSubmittedTimeout = true };
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        await Assert.ThrowsAsync<NotSubmittedTimeoutException>(() => session.InvokeAsync("WriteAnalogF64", "{}", 1000, default));
        Assert.Equal(RemoteState.Active, session.State);
        await session.CloseAsync(1000, default);
        Assert.Equal(1, wire.Clears);
    }
    [Fact]
    public async Task Retired_queued_operation_does_not_submit_rpc_after_gate_released()
    {
        var wire = new FakeWire { PendingCall = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        var first = session.InvokeAsync("StartTask", "{}", 1000, default);
        bool retired = false;
        var second = session.InvokeAsync("StopTask", "{}", 1000, default, () => { if (retired) throw new InvalidOperationException("epoch retired"); });
        retired = true;
        wire.PendingCall.SetResult("{}");
        await first;
        await Assert.ThrowsAnyAsync<Exception>(() => second);
        Assert.Equal(1, wire.Calls);
    }
    [Fact]
    public async Task Owned_session_is_cleared_once_after_confirmed_calls()
    {
        var wire = new FakeWire();
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        Assert.StartsWith("xtest-", session.RemoteName);
        Assert.Equal("{\"status\":0}", await session.InvokeAsync("StartTask", "{}", 1000, default));
        await session.CloseAsync(1000, default);
        await session.CloseAsync(1000, default);
        Assert.Equal(RemoteState.Closed, session.State);
        Assert.Equal(1, wire.Clears);
    }

    [Fact]
    public async Task A_timed_out_native_call_cannot_be_cleared_or_retried()
    {
        var wire = new FakeWire { PendingCall = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Xnet, "{}", 1000, default);
        await Assert.ThrowsAsync<TimeoutException>(() => session.InvokeAsync("WriteFrame", "{}", 30, default));
        Assert.Equal(RemoteState.Unknown, session.State);
        await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync(100, default));
        Assert.Equal(0, wire.Clears);
        wire.PendingCall.SetResult("{}");
        await Assert.ThrowsAnyAsync<Exception>(() => session.InvokeAsync("WriteFrame", "{}", 100, default));
        Assert.Equal(1, wire.Calls);
    }

    [Fact]
    public async Task A_session_reported_as_attached_is_never_cleared()
    {
        var wire = new FakeWire { IsNew = false };
        await Assert.ThrowsAnyAsync<Exception>(() => RemoteSession.OpenAsync(wire, NiDriver.Xnet, "{}", 1000, default));
        Assert.Equal(0, wire.Clears);
    }

    [Fact]
    public async Task A_failed_clear_is_never_retried_as_proof_of_release()
    {
        var wire = new FakeWire { ClearFails = true };
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync(1000, default));
        await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync(1000, default));
        Assert.Equal(1, wire.Clears);
        Assert.Equal(RemoteState.Unknown, session.State);
    }

    [Fact]
    public async Task Caller_cancellation_does_not_imply_remote_write_was_cancelled()
    {
        var wire = new FakeWire { PendingCall = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Xnet, "{}", 1000, default);
        using var stop = new CancellationTokenSource();
        var call = session.InvokeAsync("WriteFrame", "{}", 1000, stop.Token);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Equal(RemoteState.Unknown, session.State);
        wire.PendingCall.SetResult("{}");
        Assert.Equal(0, wire.Clears);
    }

    [Fact]
    public async Task Synchronous_wire_failure_after_admission_keeps_session_unknown()
    {
        var wire = new FakeWire { ThrowSynchronously = true };
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Xnet, "{}", 1000, default);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => session.InvokeAsync("WriteFrame", "{}", 1000, default));
        Assert.Contains(session.RemoteName, error.Message);
        Assert.Equal(RemoteState.Unknown, session.State);
        await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync(100, default));
        Assert.Equal(0, wire.Clears);
    }

    [Fact]
    public async Task Cancellation_before_admission_never_calls_driver()
    {
        var wire = new FakeWire();
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        using var token = new CancellationTokenSource(); token.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.InvokeAsync("StartTask", "{}", 1000, token.Token));
        Assert.Equal(0, wire.Calls);
        Assert.Equal(RemoteState.Active, session.State);
        await session.CloseAsync(1000, default);
    }

    private sealed class FakeWire : IRemoteWire
    {
        internal bool IsNew = true, ClearFails, ThrowSynchronously, NotSubmittedTimeout;
        internal bool CancelClearOnToken;
        internal int Calls, Clears;
        internal TaskCompletionSource<string>? PendingCall;
        internal TaskCompletionSource? PendingClear;
        internal CancellationToken OutboundToken;
        internal Action? CancellationCallback;
        internal Action? ClearCancellationCallback, OnClear;
        public Task<OpenReply> OpenAsync(NiDriver driver, string name, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null)
            => Task.FromResult(new OpenReply(name, IsNew, 0));
        public Task<string> InvokeAsync(NiDriver driver, string name, string method, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null)
        { Calls++; OutboundToken = ct; if (CancellationCallback != null) ct.Register(CancellationCallback); if (ThrowSynchronously) throw new InvalidOperationException("transport failed"); if (NotSubmittedTimeout) return Task.FromException<string>(new NotSubmittedTimeoutException("未提交")); return PendingCall?.Task ?? Task.FromResult("{\"status\":0}"); }
        public Task ClearAsync(NiDriver driver, string name, int timeoutMs, CancellationToken ct)
        { Clears++; if (ClearCancellationCallback != null) ct.Register(ClearCancellationCallback); if (CancelClearOnToken && PendingClear != null) ct.Register(() => PendingClear.TrySetCanceled(ct)); OnClear?.Invoke(); return ClearFails ? Task.FromException(new InvalidOperationException("clear failed")) : PendingClear?.Task ?? Task.CompletedTask; }
    }
}
