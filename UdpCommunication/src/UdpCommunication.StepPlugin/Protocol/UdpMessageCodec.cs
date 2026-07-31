using System.Globalization;
using System.Text;

namespace UdpCommunication.StepPlugin.Protocol;

public static class UdpMessageCodec
{
    public static byte[] Encode(string value, UdpPacketFormat format)
    {
        ArgumentNullException.ThrowIfNull(value);

        return format switch
        {
            UdpPacketFormat.Utf8Text => Encoding.UTF8.GetBytes(value),
            UdpPacketFormat.Hexadecimal => EncodeHexadecimal(value),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    public static string Decode(ReadOnlySpan<byte> bytes, UdpPacketFormat format)
    {
        return format switch
        {
            UdpPacketFormat.Utf8Text => Encoding.UTF8.GetString(bytes),
            UdpPacketFormat.Hexadecimal => string.Join(" ", Convert.ToHexString(bytes).Chunk(2).Select(static pair => new string(pair))),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    public static bool IsMatch(string actual, string expected, UdpReplyMatchMode mode)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expected);

        return mode switch
        {
            UdpReplyMatchMode.Exact => string.Equals(actual, expected, StringComparison.Ordinal),
            UdpReplyMatchMode.Contains => actual.Contains(expected, StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private static byte[] EncodeHexadecimal(string value)
    {
        var normalized = string.Concat(value.Where(static character => !char.IsWhiteSpace(character)));
        if (normalized.Length % 2 != 0)
        {
            throw new FormatException("十六进制报文格式无效");
        }

        var result = new byte[normalized.Length / 2];
        try
        {
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = byte.Parse(
                    normalized.AsSpan(index * 2, 2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture);
            }
        }
        catch (FormatException)
        {
            throw new FormatException("十六进制报文格式无效");
        }

        return result;
    }
}
