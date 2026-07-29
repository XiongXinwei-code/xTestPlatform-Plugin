using System.Collections.Concurrent;
using System.IO.Ports;

namespace SerialPortPlugin.Services;

/// <summary>
/// 静态串口实例管理器，按端口名缓存 SerialPort 实例，供跨步骤共享。
/// </summary>
public static class SerialPortManager
{
    private static readonly ConcurrentDictionary<string, SerialPort> _ports = new(StringComparer.OrdinalIgnoreCase);

    public static SerialPort Open(string portName, int baudRate, int dataBits, StopBits stopBits, Parity parity, int readTimeout, int writeTimeout)
    {
        if (_ports.TryGetValue(portName, out var existing) && existing.IsOpen)
            throw new InvalidOperationException($"串口 {portName} 已打开");

        var port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
        {
            ReadTimeout = readTimeout,
            WriteTimeout = writeTimeout
        };
        port.Open();
        _ports[portName] = port;
        return port;
    }

    public static void Close(string portName)
    {
        if (_ports.TryRemove(portName, out var port))
        {
            if (port.IsOpen) port.Close();
            port.Dispose();
        }
    }

    public static SerialPort Get(string portName)
    {
        if (_ports.TryGetValue(portName, out var port) && port.IsOpen)
            return port;
        throw new InvalidOperationException($"串口 {portName} 未打开，请先执行 SerialPortOpen 步骤");
    }

    public static void CloseAll()
    {
        foreach (var kvp in _ports)
        {
            try { if (kvp.Value.IsOpen) kvp.Value.Close(); kvp.Value.Dispose(); } catch { }
        }
        _ports.Clear();
    }
}
