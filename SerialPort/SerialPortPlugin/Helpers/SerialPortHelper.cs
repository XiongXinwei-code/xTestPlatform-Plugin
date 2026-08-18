using SerialPort.Models;
using SysSerialPort = System.IO.Ports.SerialPort;

namespace SerialPort.Helpers;

public static class SerialPortHelper
{
    private const string PortKeyPrefix = "__SerialPort_";

    public static string GetPortKey(string portName) => $"{PortKeyPrefix}{portName}";

    /// <summary>
    /// 将用户配置的终止符归一化为真实字符（同时支持转义文本 \n、\r、\r\n、\t 与真实字符）；
    /// 为空时保持为空（表示不按终止符结束，读到超时为止）
    /// </summary>
    public static string NormalizeTerminator(string? terminator)
    {
        if (string.IsNullOrEmpty(terminator))
            return string.Empty;
        return terminator.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\t", "\t");
    }

    public static byte[] ConvertToBytes(string data, SerialPortDataFormat format)
    {
        return format switch
        {
            SerialPortDataFormat.Hex => HexToBytes(data),
            SerialPortDataFormat.Bin => BinToBytes(data),
            SerialPortDataFormat.String => System.Text.Encoding.UTF8.GetBytes(data),
            _ => System.Text.Encoding.UTF8.GetBytes(data)
        };
    }

    public static string ConvertFromBytes(byte[] data, SerialPortDataFormat format)
    {
        return format switch
        {
            SerialPortDataFormat.Hex => BitConverter.ToString(data).Replace("-", " "),
            SerialPortDataFormat.Bin => string.Join(" ", data.Select(b => Convert.ToString(b, 2).PadLeft(8, '0'))),
            SerialPortDataFormat.String => System.Text.Encoding.UTF8.GetString(data),
            _ => System.Text.Encoding.UTF8.GetString(data)
        };
    }

    private static byte[] HexToBytes(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "").Replace("0x", "").Replace("0X", "");
        if (hex.Length % 2 != 0)
            hex = "0" + hex;

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    private static byte[] BinToBytes(string bin)
    {
        bin = bin.Replace(" ", "");
        if (bin.Length % 8 != 0)
            bin = bin.PadLeft((bin.Length / 8 + 1) * 8, '0');

        var bytes = new byte[bin.Length / 8];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(bin.Substring(i * 8, 8), 2);
        return bytes;
    }

    /// <summary>
    /// 带真实超时的串口写入。
    /// SerialStream 的 WriteAsync 在 Windows 上会忽略 CancellationToken 且不受 WriteTimeout 约束，
    /// 硬件流控未就绪时会永久阻塞；同步 Write 才会遵守 WriteTimeout 并抛出 TimeoutException。
    /// 超时时抛出 <see cref="TimeoutException"/>，用户取消时抛出 <see cref="OperationCanceledException"/>。
    /// </summary>
    public static async Task WriteWithTimeoutAsync(
        SysSerialPort port, byte[] data, int timeoutMs, CancellationToken cancellationToken)
    {
        port.WriteTimeout = timeoutMs;
        await RunWithTimeoutAsync(
            () => { port.Write(data, 0, data.Length); return 0; },
            timeoutMs, cancellationToken);
    }

    /// <summary>
    /// 带真实超时的串口读取，返回本次读到的字节数（同步 Read 至少返回 1 字节，超时抛 TimeoutException）。
    /// 原因同 <see cref="WriteWithTimeoutAsync"/>：SerialStream.ReadAsync 不响应 CancellationToken。
    /// </summary>
    public static Task<int> ReadWithTimeoutAsync(
        SysSerialPort port, byte[] buffer, int offset, int count, int timeoutMs, CancellationToken cancellationToken)
    {
        port.ReadTimeout = timeoutMs;
        return RunWithTimeoutAsync(() => port.Read(buffer, offset, count), timeoutMs, cancellationToken);
    }

    /// <summary>
    /// 在后台线程执行阻塞式串口操作，并附加一层软超时兜底：
    /// 即使底层驱动无视 ReadTimeout/WriteTimeout，步骤也能超时返回而不会卡死整条序列。
    /// </summary>
    private static async Task<int> RunWithTimeoutAsync(
        Func<int> operation, int timeoutMs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var task = Task.Run(operation, CancellationToken.None);

        // 留出余量，优先让驱动层自身的超时机制抛出 TimeoutException
        var guard = timeoutMs > 0
            ? TimeSpan.FromMilliseconds(timeoutMs + 1000)
            : Timeout.InfiniteTimeSpan;

        try
        {
            return await task.WaitAsync(guard, cancellationToken);
        }
        catch (TimeoutException)
        {
            // 底层未按时返回，标记任务异常避免 UnobservedTaskException
            _ = task.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            throw new TimeoutException($"串口操作超时({timeoutMs}ms)，端口无响应");
        }
    }
}
