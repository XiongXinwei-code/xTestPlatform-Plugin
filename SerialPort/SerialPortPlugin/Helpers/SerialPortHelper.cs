using SerialPort.Models;

namespace SerialPort.Helpers;

public static class SerialPortHelper
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, object> RegistryLocks = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Dictionary<string, long>> CloseGenerations = new();
    public static object RegistryLock(object resources) => RegistryLocks.GetValue(resources, static _ => new object());
    internal static long GetCloseGeneration(object resources, string key)
    {
        lock (RegistryLock(resources)) return CloseGenerations.GetOrCreateValue(resources).GetValueOrDefault(key);
    }
    internal static void MarkCloseRequested(object resources, string key)
    {
        lock (RegistryLock(resources))
        {
            var generations = CloseGenerations.GetOrCreateValue(resources);
            generations[key] = generations.GetValueOrDefault(key) + 1;
        }
    }
    internal static void ThrowIfOpenSuperseded(object resources, string key, long generation)
    {
        if (GetCloseGeneration(resources, key) != generation)
            throw new InvalidOperationException("串口建立期间收到同名关闭请求，旧代次不得迟到登记");
    }
    private const string PortKeyPrefix = "__SerialPort_";

    public static string GetPortKey(string portName) => $"{PortKeyPrefix}{portName}";

    public static string GetPhysicalPortKey(string portName)
    {
        var name = portName.Trim().TrimEnd(':');
        if (name.StartsWith(@"\\.\", StringComparison.Ordinal)) name = name[4..];
        if (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) && int.TryParse(name.AsSpan(3), out int number)) name = "COM" + number;
        if (name.Length == 0) throw new ArgumentException("串口物理名称不能为空");
        return "SerialPort:" + name.ToUpperInvariant();
    }
    public static int RemainingBudget(long startedAt, int timeoutMs)
    {
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        double remaining = timeoutMs - System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        if (remaining <= 0) throw new TimeoutException("串口打开/关闭总期限已到，旧资源收尾不另加固定等待");
        return (int)Math.Ceiling(remaining);
    }

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
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "不支持的串口数据格式，未发送任何字节")
        };
    }

    public static string ConvertFromBytes(byte[] data, SerialPortDataFormat format)
    {
        return format switch
        {
            SerialPortDataFormat.Hex => BitConverter.ToString(data).Replace("-", " "),
            SerialPortDataFormat.Bin => string.Join(" ", data.Select(b => Convert.ToString(b, 2).PadLeft(8, '0'))),
            SerialPortDataFormat.String => System.Text.Encoding.UTF8.GetString(data),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "不支持的串口数据格式")
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

}
