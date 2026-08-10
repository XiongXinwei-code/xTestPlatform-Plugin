using System.Globalization;

namespace Ethernet.SomeIP;

/// <summary>SOME/IP 辅助解析工具。</summary>
public static class SomeIpHelper
{
    /// <summary>解析 16 位 ID（支持 0x 前缀十六进制或十进制）。</summary>
    public static ushort ParseId(string text, string fieldName)
    {
        var t = (text ?? string.Empty).Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (ushort.TryParse(t[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
                return hex;
        }
        else if (ushort.TryParse(t, out var dec))
        {
            return dec;
        }
        throw new FormatException($"{fieldName} 格式无效: '{text}'，应为十进制或 0x 前缀十六进制");
    }

    /// <summary>解析 8 位值（支持 0x 前缀十六进制或十进制）。</summary>
    public static byte ParseByte(string text, string fieldName)
    {
        var v = ParseId(text, fieldName);
        if (v > byte.MaxValue)
            throw new FormatException($"{fieldName} 超出范围(0~255): '{text}'");
        return (byte)v;
    }

    /// <summary>解析端口号。</summary>
    public static int ParsePort(string text, string fieldName)
    {
        var t = (text ?? string.Empty).Trim();
        if (!int.TryParse(t, out var port) || port < 1 || port > 65535)
            throw new FormatException($"{fieldName} 端口无效: '{text}'，应为 1~65535");
        return port;
    }

    /// <summary>解析十六进制负载字符串（允许空格/连字符分隔，可为空）。</summary>
    public static byte[] ParsePayload(string text)
    {
        var clean = (text ?? string.Empty).Replace(" ", "").Replace("-", "").Replace(",", "").Trim();
        if (clean.Length == 0) return [];
        if (clean.Length % 2 != 0)
            throw new FormatException($"Payload 十六进制长度必须为偶数: '{text}'");
        var result = new byte[clean.Length / 2];
        for (int i = 0; i < result.Length; i++)
            result[i] = byte.Parse(clean.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return result;
    }

    /// <summary>字节数组转空格分隔十六进制字符串。</summary>
    public static string ToHex(byte[] data)
        => data.Length == 0 ? string.Empty : BitConverter.ToString(data).Replace("-", " ");
}
