using System.Diagnostics;
using System.Text.Json.Serialization;
using Grpc.Core;

namespace NiGrpc.Runtime;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NiDriver { Daqmx, Xnet }
internal enum RemoteState { Opening, Active, Unknown, Closed }
internal sealed record OpenReply(string Name, bool IsNew, int Status);
internal sealed class LocalInputException(string message, Exception? inner = null) : Exception(message, inner);
internal sealed class NotSubmittedCanceledException(string message, Exception inner, CancellationToken ct) : OperationCanceledException(message, inner, ct);
internal sealed class NotSubmittedTimeoutException(string message) : TimeoutException(message);
internal sealed class CloseQueueTimeoutException(string message) : TimeoutException(message);
internal sealed class NiResponseException(string message) : Exception(message);
internal sealed class SessionOwnershipException(string message) : InvalidOperationException(message);
internal sealed class ConfirmedCleanupPendingException(string message, Exception inner) : Exception(message, inner);
internal interface IRemoteWire
{
    Task<OpenReply> OpenAsync(NiDriver driver, string name, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null);
    Task<string> InvokeAsync(NiDriver driver, string name, string method, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null);
    Task ClearAsync(NiDriver driver, string name, int timeoutMs, CancellationToken ct);
}

internal sealed class RemoteSession
{
    private readonly IRemoteWire _wire;
    private readonly NiDriver _driver;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();
    private RemoteState _state = RemoteState.Opening;
    private bool _closing;
    private bool _clearConfirmed;
    private readonly Action<Task>? _track;
    private readonly Action<string>? _unknown;
    internal RemoteState State { get { lock (_sync) return _state; } }
    internal string RemoteName { get; }
    internal NiDriver Driver => _driver;
    internal bool ClearConfirmed { get { lock (_sync) return _clearConfirmed; } }

    private RemoteSession(IRemoteWire wire, NiDriver driver, Action<Task>? track, Action<string>? unknown)
    { _wire = wire; _driver = driver; _track = track; _unknown = unknown; RemoteName = "xtest-" + Guid.NewGuid().ToString("N"); }

    internal static async Task<RemoteSession> OpenAsync(IRemoteWire wire, NiDriver driver, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null, Task? scopeStop = null, Action<Task>? track = null, Action<string>? unknown = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        if (ct.IsCancellationRequested)
            throw new NotSubmittedCanceledException("创建已取消，远端 RPC 未提交", new OperationCanceledException(ct), ct);
        if (!Enum.IsDefined(driver)) throw new LocalInputException("未知 NI 驱动类型，未提交调用");
        var session = new RemoteSession(wire, driver, track, unknown);
        Task<OpenReply>? actual = null;
        var outbound = new OutboundCancellation(ct, scopeStop);
        try
        {
            try { checkAdmission?.Invoke(); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ct.IsCancellationRequested) { throw new OperationCanceledException("创建已取消", ex, ct); }
            catch (Exception ex) { throw new LocalInputException("执行作用域已退出或连接正在关闭；创建 RPC 未提交", ex); }
            ct.ThrowIfCancellationRequested();
            outbound.Token.ThrowIfCancellationRequested();
            actual = wire.OpenAsync(driver, session.RemoteName, json, timeoutMs, outbound.Token, checkAdmission);
            var reply = await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
            if (!reply.IsNew || reply.Name != session.RemoteName || reply.Status < 0)
                throw new SessionOwnershipException($"NI 返回的会话不是本次新建实例，未接管也不会清理；远端名称={session.RemoteName}，状态={reply.Status}");
            lock (session._sync) session._state = RemoteState.Active;
            return session;
        }
        catch (OperationCanceledException ex) when (actual == null && ct.IsCancellationRequested)
        {
            outbound.RequestCancel();
            throw new NotSubmittedCanceledException("创建已取消，远端 RPC 未提交", ex, ct);
        }
        catch (OperationCanceledException ex) when (actual == null)
        {
            outbound.RequestCancel();
            throw new LocalInputException("执行作用域已退出，创建 RPC 未提交", ex);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            outbound.RequestCancel();
            throw new OperationCanceledException($"创建已提交，远端结果未知；会话名称={session.RemoteName}", ex, ct);
        }
        catch (TimeoutException ex)
        {
            outbound.RequestCancel();
            throw new TimeoutException($"创建超过 {timeoutMs} ms，远端结果未知；会话名称={session.RemoteName}", ex);
        }
        catch (RpcException ex) when (ct.IsCancellationRequested)
        { throw new OperationCanceledException($"创建 RPC 已取消，远端结果未知；会话名称={session.RemoteName}；{RpcDetails(ex)}", ex, ct); }
        catch (LocalInputException) { throw; }
        catch (RpcException ex)
        { throw new InvalidOperationException($"创建 RPC 失败，远端结果可能未知；会话名称={session.RemoteName}；{RpcDetails(ex)}", ex); }
        catch (Exception ex) when (ex is not SessionOwnershipException)
        { throw new InvalidOperationException($"创建 RPC 失败，远端结果可能未知；会话名称={session.RemoteName}", ex); }
        finally { var finish = FinishAfterActual(actual, outbound, null); track?.Invoke(finish); }
    }

    internal async Task<string> InvokeAsync(string method, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null, Task? scopeStop = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        ct.ThrowIfCancellationRequested();
        var clock = Stopwatch.StartNew();
        if (!await _gate.WaitAsync(timeoutMs, ct).ConfigureAwait(false))
            throw new TimeoutException($"等待 NI 会话操作排队超过 {timeoutMs} ms；没有提交新调用");
        Task<string>? actual = null;
        OutboundCancellation? outbound = null;
        try
        {
            lock (_sync)
                if (_closing || _state != RemoteState.Active)
                    throw new InvalidOperationException($"NI 会话不可用：{_state}；远端名称={RemoteName}");
            ct.ThrowIfCancellationRequested();
            CheckAdmission();
            int remaining = Remaining(clock, timeoutMs);
            try
            {
                outbound = new OutboundCancellation(ct, scopeStop);
                ct.ThrowIfCancellationRequested();
                outbound.Token.ThrowIfCancellationRequested();
                actual = _wire.InvokeAsync(_driver, RemoteName, method, json, remaining, outbound.Token, CheckAdmission);
                return await actual.WaitAsync(TimeSpan.FromMilliseconds(remaining), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            { if (actual != null) MarkUnknown(); outbound?.RequestCancel(); throw; }
            catch (OperationCanceledException ex) when (actual == null)
            { outbound?.RequestCancel(); throw new LocalInputException("执行作用域已退出，目标 RPC 未提交", ex); }
            catch (NotSubmittedTimeoutException) { throw; }
            catch (TimeoutException ex)
            { MarkUnknown(); outbound?.RequestCancel(); throw new TimeoutException($"NI 调用超时，远端结果未知且不会自动重发；会话名称={RemoteName}", ex); }
            catch (RpcException ex) when (ct.IsCancellationRequested)
            { MarkUnknown(); throw new OperationCanceledException($"NI 调用已取消，远端结果未知；会话名称={RemoteName}；{RpcDetails(ex)}", ex, ct); }
            catch (RpcException ex)
            { MarkUnknown(); throw new InvalidOperationException($"NI RPC 失败，远端结果未知；会话名称={RemoteName}；{RpcDetails(ex)}", ex); }
            catch (LocalInputException) { throw; }
            catch (NiResponseException) { throw; }
            catch (Exception ex)
            { MarkUnknown(); throw new InvalidOperationException($"NI 调用异常，远端结果未知；会话名称={RemoteName}", ex); }
        }
        finally { var finish = FinishAfterActual(actual, outbound, () => _gate.Release()); _track?.Invoke(finish); }
        void CheckAdmission()
        {
            checkAdmission?.Invoke();
            lock (_sync)
                if (_closing || _state != RemoteState.Active)
                    throw new InvalidOperationException($"NI 会话正在关闭或不可用，禁止提交新调用；远端名称={RemoteName}");
        }
    }

    internal async Task CloseAsync(int timeoutMs, CancellationToken ct, Action? checkAdmission = null, Task? scopeStop = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        ct.ThrowIfCancellationRequested();
        var clock = Stopwatch.StartNew();
        lock (_sync)
        {
            if (_state == RemoteState.Closed) return;
            if (_state == RemoteState.Unknown) throw Unknown();
            _closing = true;
        }
        if (!await _gate.WaitAsync(timeoutMs, ct).ConfigureAwait(false))
            throw new CloseQueueTimeoutException($"NI 会话仍有调用在退出，未提交 Clear；远端名称={RemoteName}");
        Task? actual = null;
        OutboundCancellation? outbound = null;
        bool finishStarted = false;
        try
        {
            lock (_sync)
            {
                if (_state == RemoteState.Closed) return;
                if (_state == RemoteState.Unknown) throw Unknown();
            }
            ct.ThrowIfCancellationRequested();
            CheckCloseAdmission();
            int remaining;
            try { remaining = Remaining(clock, timeoutMs); }
            catch (TimeoutException) { throw new CloseQueueTimeoutException($"NI 会话关闭期限在提交 Clear 前届满；远端名称={RemoteName}"); }
            try
            {
                outbound = new OutboundCancellation(ct, scopeStop);
                ct.ThrowIfCancellationRequested();
                outbound.Token.ThrowIfCancellationRequested();
                CheckCloseAdmission();
                actual = _wire.ClearAsync(_driver, RemoteName, remaining, outbound.Token);
                await actual.WaitAsync(TimeSpan.FromMilliseconds(remaining), ct).ConfigureAwait(false);
            }
            catch (RpcException ex) when (ct.IsCancellationRequested)
            { MarkUnknown(); throw new OperationCanceledException($"NI 释放已取消，远端状态未知；会话名称={RemoteName}；{RpcDetails(ex)}", ex, ct); }
            catch (RpcException ex)
            { MarkUnknown(); throw new InvalidOperationException($"NI 释放失败，远端状态未知；会话名称={RemoteName}；{RpcDetails(ex)}", ex); }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            { if (actual != null) MarkUnknown(); outbound?.RequestCancel(); throw new OperationCanceledException(actual == null
                ? $"NI 释放已取消，Clear 未提交；会话名称={RemoteName}"
                : $"NI 释放已取消，远端状态未知；会话名称={RemoteName}", ex, ct); }
            catch (OperationCanceledException ex) when (actual == null)
            { outbound?.RequestCancel(); throw new LocalInputException("执行作用域已退出，Clear 未提交", ex); }
            catch (OperationCanceledException ex)
            { MarkUnknown(); outbound?.RequestCancel(); throw new InvalidOperationException($"NI Clear 已提交但执行作用域已退出，远端状态未知；会话名称={RemoteName}", ex); }
            catch (TimeoutException ex)
            { MarkUnknown(); outbound?.RequestCancel(); throw new TimeoutException($"NI 释放超时，远端状态未知；会话名称={RemoteName}", ex); }
            catch (LocalInputException) { throw; }
            catch { MarkUnknown(); throw; }
            var finish = FinishAfterActual(actual, outbound, () =>
            {
                lock (_sync) { _state = RemoteState.Closed; _gate.Release(); }
            });
            finishStarted = true;
            try { _track?.Invoke(finish); }
            catch (Exception ex) { _unknown?.Invoke($"远端会话 {RemoteName} 的本地收尾跟踪失败：{ex.Message}"); throw; }
            lock (_sync) _clearConfirmed = true;
            try
            {
                if (finish.IsCompleted) { await finish.ConfigureAwait(false); _ = Remaining(clock, timeoutMs); }
                else await finish.WaitAsync(TimeSpan.FromMilliseconds(Remaining(clock, timeoutMs)), ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            { throw new OperationCanceledException(finish.IsCompletedSuccessfully
                ? $"NI Clear 和本地回调均已退出，但关闭步骤已取消；会话名称={RemoteName}"
                : $"NI Clear 已确认，但本地回调仍在退出；会话名称={RemoteName}", ex, ct); }
            catch (TimeoutException ex)
            { throw new TimeoutException(finish.IsCompletedSuccessfully
                ? $"NI Clear 和本地回调均已退出，但关闭步骤超过 {timeoutMs} ms 期限；会话名称={RemoteName}"
                : $"NI Clear 已确认，但本地回调在 {timeoutMs} ms 期限内未退出；会话名称={RemoteName}", ex); }
        }
        finally { if (!finishStarted) { var finish = FinishAfterActual(actual, outbound, () => _gate.Release()); _track?.Invoke(finish); } }
        void CheckCloseAdmission()
        {
            try { checkAdmission?.Invoke(); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new LocalInputException("执行作用域已退出，Clear 未提交", ex); }
        }
    }

    private void MarkUnknown() { lock (_sync) _state = RemoteState.Unknown; _unknown?.Invoke($"远端会话 {RemoteName} 的调用或释放结果未知"); }
    internal async Task WaitForIdleAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        _gate.Release();
    }
    private InvalidOperationException Unknown() => new($"NI 远端会话结果未知，不能自动重发或清理；会话名称={RemoteName}");
    private static int Remaining(Stopwatch clock, int limit) => clock.ElapsedMilliseconds < limit
        ? limit - (int)clock.ElapsedMilliseconds : throw new TimeoutException($"等待超过 {limit} ms，未提交新调用");
    private static Task FinishAfterActual(Task? actual, OutboundCancellation? outbound, Action? release)
    {
        var finished = outbound?.FinishAsync(actual) ?? Task.CompletedTask;
        var released = finished.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully)
            {
                Trace.TraceError($"NI gRPC 取消收尾未完成，保留会话门：{t.Exception}");
                throw new InvalidOperationException("NI gRPC 取消收尾未完成，已保留会话门", t.Exception);
            }
            release?.Invoke();
        }, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        _ = released.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return released;
    }
    private static string RpcDetails(RpcException ex)
    {
        var details = ex.Trailers.Where(x => !x.IsBinary && (x.Key.Equals("ni-error", StringComparison.OrdinalIgnoreCase) ||
            x.Key.Equals("ni-samps-per-chan-read", StringComparison.OrdinalIgnoreCase) ||
            x.Key.Equals("ni-samps-per-chan-written", StringComparison.OrdinalIgnoreCase)))
            .Select(x => $"{x.Key}={x.Value}");
        return $"gRPC={ex.StatusCode}，详情={ex.Status.Detail}；{string.Join("，", details)}";
    }
}
