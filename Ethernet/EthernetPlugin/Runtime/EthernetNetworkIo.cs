using System.Net;
using System.Net.Sockets;

namespace Ethernet.Runtime;

/// <summary>异步网络入口；调用返回后才由上层释放实际 socket，不以停止等待冒充 I/O 退出。</summary>
internal interface IEthernetNetworkIo
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct);
    Task ConnectTcpAsync(TcpClient client, string host, int port, CancellationToken ct);
    Task WriteTcpAsync(NetworkStream stream, byte[] data, CancellationToken ct);
    Task SendUdpAsync(UdpClient client, IPEndPoint endpoint, byte[] data, CancellationToken ct);
}

internal sealed class EthernetNetworkIo : IEthernetNetworkIo
{
    internal static readonly IEthernetNetworkIo Instance = new EthernetNetworkIo();
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Dns.GetHostAddressesAsync(host, ct);
    public async Task ConnectTcpAsync(TcpClient client, string host, int port, CancellationToken ct) =>
        await client.ConnectAsync(host, port, ct).ConfigureAwait(false);
    public async Task WriteTcpAsync(NetworkStream stream, byte[] data, CancellationToken ct) =>
        await stream.WriteAsync(data, ct).ConfigureAwait(false);
    public async Task SendUdpAsync(UdpClient client, IPEndPoint endpoint, byte[] data, CancellationToken ct)
    {
        if (client.Client.Connected) await client.SendAsync(data, ct).ConfigureAwait(false);
        else await client.SendAsync(data, endpoint, ct).ConfigureAwait(false);
    }

    internal static async Task<IPEndPoint> ResolveEndpointAsync(IEthernetNetworkIo network, string host,
        int port, AddressFamily family, EthernetOperationBudget budget)
    {
        budget.Phase = "解析目标地址";
        budget.Check();
        var addresses = IPAddress.TryParse(host, out var literal) ? [literal]
            : await network.ResolveAsync(host, budget.Token).ConfigureAwait(false);
        budget.Check(); // 迟到解析不能继续提交；与发送/响应共用原来的单调预算。
        var address = addresses.FirstOrDefault(a => a.AddressFamily == family)
            ?? throw new SocketException((int)SocketError.AddressFamilyNotSupported);
        return new IPEndPoint(address, port);
    }
}
