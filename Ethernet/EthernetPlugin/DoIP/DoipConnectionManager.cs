using System.Net.Sockets;
using Ethernet.Runtime;
using xTestPlatform.Core.Engine;

namespace Ethernet.DoIP;

/// <summary>执行级 DoIP 会话池，同名操作按代次管理，防止迟到登记。</summary>
public static class DoipConnectionManager
{
    private static readonly NamedConnectionPool<DoipClient> Legacy = new();
    private static NamedConnectionPool<DoipClient> Pool(IResourceRegistry? resources) =>
        NamedConnectionPool<DoipClient>.For(resources, "Ethernet.DoIP.ConnectionPool", Legacy);

    public static Task<DoipClient> ConnectAsync(string sessionName, string host, int port,
        ushort sourceAddress, byte activationType, int timeoutMs, CancellationToken ct) =>
        ConnectAsync(sessionName, host, port, sourceAddress, activationType, timeoutMs, ct, null);

    public static Task<DoipClient> ConnectAsync(string sessionName, string host, int port,
        ushort sourceAddress, byte activationType, int timeoutMs, CancellationToken ct,
        IResourceRegistry? resources) =>
        ConnectAsync(sessionName, host, port, sourceAddress, activationType, timeoutMs, ct, resources, static () => { });

    internal static Task<DoipClient> ConnectAsync(string sessionName, string host, int port,
        ushort sourceAddress, byte activationType, int timeoutMs, CancellationToken ct,
        IResourceRegistry? resources, Action ensureExecution)
    {
        ensureExecution();
        return Pool(resources).OpenAsync(sessionName, async token =>
        {
            var tcp = new TcpClient();
            DoipClient? client = null;
            try
            {
                await tcp.ConnectAsync(host, port, token).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                ensureExecution();
                client = new DoipClient(tcp, sourceAddress, timeoutMs);
                await client.RoutingActivationAsync(activationType, token).ConfigureAwait(false);
                return client;
            }
            catch { if (client is null) tcp.Dispose(); else client.Dispose(); throw; }
        }, timeoutMs, ct, ensureExecution);
    }

    public static DoipClient Get(string sessionName) => Get(sessionName, null);

    public static DoipClient Get(string sessionName, IResourceRegistry? resources)
    {
        var client = Pool(resources).Get(sessionName);
        if (client.IsUsable) return client;
        throw new InvalidOperationException($"DoIP 会话 [{sessionName}] 已失效，请重新连接。");
    }

    public static Task CloseAsync(string sessionName, CancellationToken ct = default,
        IResourceRegistry? resources = null, int timeoutMs = 5000) =>
        Pool(resources).CloseAsync(sessionName, timeoutMs, ct);

    internal static Task CloseAsync(string sessionName, CancellationToken ct,
        IResourceRegistry resources, int timeoutMs, Action ensureExecution)
    {
        ct.ThrowIfCancellationRequested();
        var capturedPool = Pool(resources);
        ensureExecution();
        return capturedPool.CloseAsync(sessionName, timeoutMs, ct);
    }

    [Obsolete("步骤和异步调用方应使用 CloseAsync，避免同步等待连接任务。")]
    public static void Close(string sessionName) => CloseAsync(sessionName).GetAwaiter().GetResult();
}
