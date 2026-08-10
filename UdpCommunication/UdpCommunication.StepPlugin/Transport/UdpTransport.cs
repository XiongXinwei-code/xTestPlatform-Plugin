using System.Net;
using System.Net.Sockets;

namespace UdpCommunication.Transport;

public sealed class UdpTransport : IUdpTransport
{
    private readonly UdpClient _client;
    private bool _disposed;

    public IPEndPoint LocalEndPoint => (IPEndPoint)_client.Client.LocalEndPoint!;

    public UdpTransport(string localAddress, int localPort)
    {
        var localIp = IPAddress.Parse(localAddress);
        _client = new UdpClient(new IPEndPoint(localIp, localPort));
    }

    public async Task SendAsync(
        ReadOnlyMemory<byte> request,
        IPEndPoint remoteEndpoint,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        await _client.SendAsync(request, remoteEndpoint, cancellationToken);
    }

    public async Task<UdpTransportResult> ReceiveAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            var reply = await _client.ReceiveAsync(timeoutSource.Token);
            return new UdpTransportResult(reply.Buffer, reply.RemoteEndPoint);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("鎺ユ敹 UDP 鍥炲瓒呮椂");
        }
    }

    public async Task<UdpTransportResult> SendAndReceiveAsync(
        ReadOnlyMemory<byte> request,
        IPEndPoint remoteEndpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        await _client.SendAsync(request, remoteEndpoint, cancellationToken);
        return await ReceiveAsync(timeout, cancellationToken);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UdpTransport));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Dispose();
    }
}
