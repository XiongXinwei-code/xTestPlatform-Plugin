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
        if (timeoutMs <= 0 && timeoutMs != SysSerialPort.InfiniteTimeout)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), $"写超时必须大于 0 或为 -1（不超时），当前值: {timeoutMs}");
        port.WriteTimeout = timeoutMs;
        await RunWithTimeoutAsync(
            () => { port.Write(data, 0, data.Length); return 0; },
            timeoutMs, cancellationToken);
    }

    /// <summary>
    /// 按固定字节数或终止符读取串口数据。
    /// timeoutMs 为 -1 时表示永不超时（仅可由取消终止）；终止符按原始字节匹配，与显示格式无关。
    /// 超时或未满足结束条件时抛出 <see cref="TimeoutException"/>。
    /// </summary>
    public static async Task<byte[]> ReadFrameAsync(
        SysSerialPort port, int readBytes, string? terminatorText, int timeoutMs, string operationName, CancellationToken cancellationToken)
    {
        var infinite = timeoutMs == SysSerialPort.InfiniteTimeout;
        var deadline = infinite ? DateTime.MaxValue : DateTime.UtcNow.AddMilliseconds(timeoutMs);
        int Remaining() => infinite ? SysSerialPort.InfiniteTimeout : (int)(deadline - DateTime.UtcNow).TotalMilliseconds;

        if (readBytes > 0)
        {
            var buffer = new byte[readBytes];
            int totalRead = 0;
            try
            {
                while (totalRead < readBytes)
                {
                    var remaining = Remaining();
                    if (!infinite && remaining <= 0) break;

                    int read = await ReadWithTimeoutAsync(
                        port, buffer, totalRead, readBytes - totalRead, remaining, cancellationToken);
                    if (read == 0) break;
                    totalRead += read;
                }
            }
            catch (TimeoutException)
            {
            }

            if (totalRead < readBytes)
                throw new TimeoutException(
                    $"{operationName}超时({timeoutMs}ms): 需读 {readBytes} 字节，实际只读到 {totalRead} 字节");
            return buffer;
        }

        var terminator = NormalizeTerminator(terminatorText);
        var terminatorBytes = System.Text.Encoding.UTF8.GetBytes(terminator);
        var needTerminator = terminatorBytes.Length > 0;

        if (!needTerminator && infinite)
            throw new InvalidOperationException($"{operationName}失败: 未配置读取字节数和终止符时，读取超时不能为 -1（永不超时）");

        using var ms = new MemoryStream();
        var temp = new byte[1024];
        var matched = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = Remaining();
            if (!infinite && remaining <= 0) break;

            int read;
            try
            {
                read = await ReadWithTimeoutAsync(port, temp, 0, temp.Length, remaining, cancellationToken);
            }
            catch (TimeoutException)
            {
                break;
            }
            if (read == 0) break;
            ms.Write(temp, 0, read);

            if (needTerminator && ms.GetBuffer().AsSpan(0, (int)ms.Length).IndexOf(terminatorBytes) >= 0)
            {
                matched = true;
                break;
            }
        }

        var data = ms.ToArray();

        if (needTerminator && !matched)
            throw new TimeoutException(
                $"{operationName}超时({timeoutMs}ms): 未收到终止符，已收到 {data.Length} 字节");

        if (data.Length == 0)
            throw new TimeoutException($"{operationName}超时({timeoutMs}ms): 未收到任何数据");

        return data;
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
