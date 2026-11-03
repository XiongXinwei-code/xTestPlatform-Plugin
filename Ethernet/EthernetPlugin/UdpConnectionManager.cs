using System.Net;
using Ethernet.Runtime;
using xTestPlatform.Core.Engine;

namespace Ethernet;

/// <summary>UDP 命名连接和 TCP 分池；Open 只绑定端口，不进行远端握手。</summary>
internal static class UdpConnectionManager
{
    private static readonly NamedConnectionPool<UdpConnectionResource> Legacy = new();
    private static NamedConnectionPool<UdpConnectionResource> Pool(IResourceRegistry? resources) =>
        NamedConnectionPool<UdpConnectionResource>.For(resources, "Ethernet.UDP.ConnectionPool", Legacy);
    internal static Task<UdpConnectionResource> OpenAsync(string name, IPEndPoint local, bool broadcast,
        int timeoutMs, CancellationToken ct, IResourceRegistry resources, Action check) =>
        Pool(resources).OpenAsync(name, token =>
        {
            token.ThrowIfCancellationRequested(); check();
            return Task.FromResult(new UdpConnectionResource(local, broadcast));
        }, timeoutMs, ct, check);
    internal static UdpConnectionResource Get(string name, IResourceRegistry resources) => Pool(resources).Get(name);
    internal static Task CloseAsync(string name, int timeoutMs, CancellationToken ct, IResourceRegistry resources, Action check)
    {
        var pool = Pool(resources); check(); return pool.CloseAsync(name, timeoutMs, ct);
    }
}
