using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Ethernet;

/// <summary>全局 TCP 连接池，同一 ConnectionName 复用同一 TcpClient 实例。</summary>
public static class TcpConnectionManager
{
    private static readonly ConcurrentDictionary<string, TcpClient> _clients = new();

    /// <summary>建立并注册连接，若已存在同名连接则先关闭旧连接。</summary>
    public static async Task<TcpClient> ConnectAsync(
        string connectionName,
        string host,
        int port,
        int timeoutMs,
        CancellationToken ct)
    {
        if (_clients.TryRemove(connectionName, out var old))
        {
            old.Close();
            old.Dispose();
        }

        var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            await client.ConnectAsync(host, port, cts.Token);
        }
        catch
        {
            client.Dispose();
            throw;
        }
        _clients[connectionName] = client;
        return client;
    }

    /// <summary>获取已注册的连接，不存在时抛出异常。</summary>
    public static TcpClient Get(string connectionName)
    {
        if (_clients.TryGetValue(connectionName, out var client) && client.Connected)
            return client;
        throw new InvalidOperationException($"TCP 连接 [{connectionName}] 不存在或已断开，请先执行 Ethernet_TcpOpen 步骤。");
    }

    /// <summary>关闭并移除连接。</summary>
    public static void Close(string connectionName)
    {
        if (_clients.TryRemove(connectionName, out var client))
        {
            client.Close();
            client.Dispose();
        }
    }
}
