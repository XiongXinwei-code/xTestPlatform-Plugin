using System.IO.Ports;
using NModbus.IO;

namespace Modbus.Helpers;

internal interface ISerialPortOperations : IDisposable
{
    bool IsOpen { get; }
    int ReadTimeout { get; set; }
    int WriteTimeout { get; set; }
    int Read(byte[] buffer, int offset, int count);
    void Write(byte[] buffer, int offset, int count);
    void DiscardInBuffer();
    void DiscardOutBuffer();
    void Close();
}

/// <summary>
/// 将 System.IO.Ports.SerialPort 适配为 NModbus 所需的 IStreamResource 接口，
/// 用于 RTU 串口通信模式
/// </summary>
public sealed class SerialPortAdapter : IStreamResource, ICancellableModbusTransport
{
    private readonly ISerialPortOperations _port;
    private readonly object _abortSync = new();
    private int _closing;
    private bool _disposed;

    public SerialPortAdapter(SerialPort port) : this(new SystemPortOperations(port)) { }
    internal SerialPortAdapter(ISerialPortOperations port) => _port = port;

    public int InfiniteTimeout => SerialPort.InfiniteTimeout;
    public int ReadTimeout { get => _port.ReadTimeout; set { ThrowIfClosing(); _port.ReadTimeout = value; } }
    public int WriteTimeout { get => _port.WriteTimeout; set { ThrowIfClosing(); _port.WriteTimeout = value; } }

    public void DiscardInBuffer() { ThrowIfClosing(); _port.DiscardInBuffer(); }

    public int Read(byte[] buffer, int offset, int count) { ThrowIfClosing(); return _port.Read(buffer, offset, count); }

    public void Write(byte[] buffer, int offset, int count) { ThrowIfClosing(); _port.Write(buffer, offset, count); }

    public void CancelPendingIo()
    {
        Interlocked.Exchange(ref _closing, 1);
        lock (_abortSync)
        {
            if (_disposed) return;
            List<Exception> errors = [];
            bool open = true;
            try { open = _port.IsOpen; } catch (Exception ex) { errors.Add(ex); }
            // .NET 8 maps these public APIs to PURGE_RX/TXABORT. Close alone
            // flushes first and can wait forever for an infinite/flow-controlled write.
            if (open)
            {
                Attempt(_port.DiscardOutBuffer);
                Attempt(_port.DiscardInBuffer);
            }
            Attempt(_port.Close); // A purge/status failure must never skip legal SDK Close.
            if (errors.Count != 0) throw new AggregateException("Modbus RTU 中断或关闭请求失败；已发送字节及远端结果未知", errors);
            void Attempt(Action action) { try { action(); } catch (Exception ex) { errors.Add(ex); } }
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _closing, 1);
        lock (_abortSync)
        {
            if (_disposed) return;
            _port.Dispose();
            _disposed = true;
        }
    }

    private void ThrowIfClosing()
    {
        if (Volatile.Read(ref _closing) != 0)
            throw new InvalidOperationException("Modbus RTU 传输正在退出，禁止重试或提交新的原生 I/O");
    }

    private sealed class SystemPortOperations(SerialPort port) : ISerialPortOperations
    {
        public bool IsOpen => port.IsOpen;
        public int ReadTimeout { get => port.ReadTimeout; set => port.ReadTimeout = value; }
        public int WriteTimeout { get => port.WriteTimeout; set => port.WriteTimeout = value; }
        public int Read(byte[] buffer, int offset, int count) => port.Read(buffer, offset, count);
        public void Write(byte[] buffer, int offset, int count) => port.Write(buffer, offset, count);
        public void DiscardInBuffer() => port.DiscardInBuffer();
        public void DiscardOutBuffer() => port.DiscardOutBuffer();
        public void Close() => port.Close();
        public void Dispose() => port.Dispose();
    }
}
