using System.Net.Sockets;
using Ethernet.Runtime;
using xTestPlatform.Core.Engine;

namespace Ethernet;

/// <summary>执行级 TCP 连接池。未传资源表的旧调用保留独立兼容池。</summary>
public static class TcpConnectionManager
{
    private static readonly NamedConnectionPool<TcpClient> Legacy = new();
    private static NamedConnectionPool<TcpClient> Pool(IResourceRegistry? resources) =>
        NamedConnectionPool<TcpClient>.For(resources, "Ethernet.TCP.ConnectionPool", Legacy);

    public static Task<TcpClient> ConnectAsync(string connectionName, string host, int port,
        int timeoutMs, CancellationToken ct) => ConnectAsync(connectionName, host, port, timeoutMs, ct, null);

    public static Task<TcpClient> ConnectAsync(string connectionName, string host, int port,
        int timeoutMs, CancellationToken ct, IResourceRegistry? resources) =>
        ConnectAsync(connectionName, host, port, timeoutMs, ct, resources, static () => { });

    internal static Task<TcpClient> ConnectAsync(string connectionName, string host, int port,
        int timeoutMs, CancellationToken ct, IResourceRegistry? resources, Action ensureExecution)
    {
        ensureExecution();
        return Pool(resources).OpenAsync(connectionName, async token =>
        {
            var client = new TcpClient();
            try { await client.ConnectAsync(host, port, token).ConfigureAwait(false); return client; }
            catch { client.Dispose(); throw; }
        }, timeoutMs, ct, ensureExecution);
    }

    public static TcpClient Get(string connectionName) => Get(connectionName, null);

    public static TcpClient Get(string connectionName, IResourceRegistry? resources)
    {
        var client = Pool(resources).Get(connectionName);
        if (client.Connected) return client;
        throw new InvalidOperationException($"TCP 连接 [{connectionName}] 已断开，请重新打开。");
    }

    public static Task CloseAsync(string connectionName, CancellationToken ct = default,
        IResourceRegistry? resources = null, int timeoutMs = 5000) =>
        Pool(resources).CloseAsync(connectionName, timeoutMs, ct);

    internal static Task CloseAsync(string connectionName, CancellationToken ct,
        IResourceRegistry resources, int timeoutMs, Action ensureExecution)
    {
        ct.ThrowIfCancellationRequested();
        var capturedPool = Pool(resources);
        ensureExecution(); // Resolving the pool may have crossed SDK cleanup/new-run registration.
        return capturedPool.CloseAsync(connectionName, timeoutMs, ct);
    }

    /// <summary>兼容入口；关闭必须等待实际完成，不可在界面线程调用。</summary>
    [Obsolete("步骤和异步调用方应使用 CloseAsync，避免同步等待连接任务。")]
    public static void Close(string connectionName) => CloseAsync(connectionName).GetAwaiter().GetResult();
}
