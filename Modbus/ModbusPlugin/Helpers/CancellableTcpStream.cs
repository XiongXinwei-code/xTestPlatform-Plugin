using System.Net.Sockets;
using NModbus.IO;

namespace Modbus.Helpers;

internal interface ICancellableModbusTransport { void CancelPendingIo(); }

/// <summary>
/// NModbus IStreamResource 是同步接口；它在连接 gate 的后台事务中调用。
/// 用 .NET socket 的原生可取消异步 I/O 实现同步桥接，关闭时取消实际 I/O，而非提前销毁句柄。
/// </summary>
internal sealed class CancellableTcpStream : IStreamResource, ICancellableModbusTransport
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _stop = new();
    private int _disposed;
    public int InfiniteTimeout => Timeout.Infinite;
    public int ReadTimeout { get; set; }
    public int WriteTimeout { get; set; }
    public CancellableTcpStream(TcpClient client, int timeoutMs) { _client = client; _stream = client.GetStream(); ReadTimeout = WriteTimeout = timeoutMs; }
    public int Read(byte[] buffer, int offset, int count)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        if (ReadTimeout > 0) deadline.CancelAfter(ReadTimeout);
        try { return _stream.ReadAsync(buffer.AsMemory(offset, count), deadline.Token).AsTask().GetAwaiter().GetResult(); }
        catch (OperationCanceledException ex) when (!_stop.IsCancellationRequested) { throw new TimeoutException("Modbus TCP 原生读取超时", ex); }
    }
    public void Write(byte[] buffer, int offset, int count)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        if (WriteTimeout > 0) deadline.CancelAfter(WriteTimeout);
        try { _stream.WriteAsync(buffer.AsMemory(offset, count), deadline.Token).AsTask().GetAwaiter().GetResult(); }
        catch (OperationCanceledException ex) when (!_stop.IsCancellationRequested) { throw new TimeoutException("Modbus TCP 原生写入超时", ex); }
    }
    // TCP 使用 MBAP 事务号关联，不能另设抢读者去“清空”共享输入。
    public void DiscardInBuffer() { }
    public void CancelPendingIo() => _stop.Cancel();
    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) != 0) return; _client.Dispose(); _stop.Dispose(); }
}
