using SerialPort.Models;

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
}
