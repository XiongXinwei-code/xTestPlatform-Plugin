using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Ethernet.DoIP;

/// <summary>全局 DoIP 会话池，同一 SessionName 复用同一已激活路由的 DoipClient。</summary>
public static class DoipConnectionManager
{
    private static readonly ConcurrentDictionary<string, DoipClient> _sessions = new();

    /// <summary>建立 TCP 连接、执行路由激活并注册会话，同名旧会话会被关闭。</summary>
    public static async Task<DoipClient> ConnectAsync(
        string sessionName,
        string host,
        int port,
        ushort sourceAddress,
        byte activationType,
        int timeoutMs,
        CancellationToken ct)
    {
        if (_sessions.TryRemove(sessionName, out var old))
            old.Dispose();

        var tcp = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        await tcp.ConnectAsync(host, port, cts.Token);

        var client = new DoipClient(tcp, sourceAddress, timeoutMs);
        try
        {
            await client.RoutingActivationAsync(activationType, ct);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        _sessions[sessionName] = client;
        return client;
    }

    /// <summary>获取已注册的会话，不存在时抛出异常。</summary>
    public static DoipClient Get(string sessionName)
    {
        if (_sessions.TryGetValue(sessionName, out var client))
            return client;
        throw new InvalidOperationException($"DoIP 会话 [{sessionName}] 不存在，请先执行 DoIP_Connect 步骤。");
    }

    /// <summary>关闭并移除会话。</summary>
    public static void Close(string sessionName)
    {
        if (_sessions.TryRemove(sessionName, out var client))
            client.Dispose();
    }
}
