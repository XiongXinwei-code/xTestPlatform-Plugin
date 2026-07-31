using System.IO.Ports;
using NModbus.IO;

namespace Modbus.Helpers;

/// <summary>
/// 将 System.IO.Ports.SerialPort 适配为 NModbus 所需的 IStreamResource 接口，
/// 用于 RTU 串口通信模式
/// </summary>
public sealed class SerialPortAdapter : IStreamResource
{
    private readonly SerialPort _port;

    public SerialPortAdapter(SerialPort port)
    {
        _port = port;
    }

    public int InfiniteTimeout => SerialPort.InfiniteTimeout;
    public int ReadTimeout { get => _port.ReadTimeout; set => _port.ReadTimeout = value; }
    public int WriteTimeout { get => _port.WriteTimeout; set => _port.WriteTimeout = value; }

    public void DiscardInBuffer() => _port.DiscardInBuffer();

    public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);

    public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);

    public void Dispose() => _port.Dispose();
}
