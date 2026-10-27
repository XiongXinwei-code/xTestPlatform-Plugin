using Opc.Ua;

namespace OpcUa.Helpers;

/// <summary>SDK Session 关闭与本地释放的测试接缝；调用方拥有实际任务和等待期限。</summary>
internal sealed class OpcUaSessionShutdown(Func<bool, CancellationToken, Task<StatusCode>> closeRemote, Action dispose)
{
    private readonly object _sync = new();
    private Task? _actual;
    internal bool RemoteCloseCallSucceeded { get; private set; }
    internal Exception? Warning { get; private set; }

    internal Task CloseAsync()
    {
        lock (_sync)
        {
            // A Session.Dispose that throws may already have marked itself
            // disposed. Never treat its later no-op as proof of real release.
            if (_actual == null)
            {
                _actual = Task.Run(CloseCoreAsync);
                _ = _actual.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            return _actual;
        }
    }

    private async Task CloseCoreAsync()
    {
        Exception? remoteError = null;
        try
        {
            // The fixed SDK's default CloseAsync also disposes the Session.
            // false selects remote close without implicit channel disposal.
            var status = await closeRemote(false, CancellationToken.None).ConfigureAwait(false);
            if (StatusCode.IsBad(status)) throw new ServiceResultException(status.Code, "OPC UA 远端关闭调用返回失败");
            // Good can also mean an SDK no-op on an already disconnected
            // Session. This is not proof of server-side Session deletion.
            RemoteCloseCallSucceeded = true;
        }
        catch (Exception ex) { remoteError = ex; }
        // Reached only after the real remote-close request returned, including
        // its failure. SDK Dispose closes this Session and its local channel.
        try { dispose(); }
        catch (Exception releaseError)
        {
            if (remoteError != null)
                throw new AggregateException("OPC UA 远端关闭与本地 Session 释放均失败，保留原实例", remoteError, releaseError);
            throw;
        }
        Warning = remoteError;
    }
}
