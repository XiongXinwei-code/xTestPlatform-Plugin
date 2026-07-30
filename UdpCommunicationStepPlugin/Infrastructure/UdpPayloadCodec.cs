using System.Text;
using UdpCommunicationStepPlugin.Setting;

namespace UdpCommunicationStepPlugin.Infrastructure;

public static class UdpPayloadCodec
{
    public static byte[] Encode(string payload, UdpDataFormat format)
    {
        payload ??= string.Empty;

        return format switch
        {
            UdpDataFormat.Utf8Text => Encoding.UTF8.GetBytes(payload),
            UdpDataFormat.Hex => DecodeHex(payload),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported UDP data format.")
        };
    }

    public static string Decode(byte[] bytes, UdpDataFormat format)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        return format switch
        {
            UdpDataFormat.Utf8Text => Encoding.UTF8.GetString(bytes),
            UdpDataFormat.Hex => Convert.ToHexString(bytes),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported UDP data format.")
        };
    }

    private static byte[] DecodeHex(string payload)
    {
        var normalized = string.Concat(payload.Where(character => !char.IsWhiteSpace(character)));
        if (normalized.Length % 2 != 0)
        {
            throw new FormatException("A hexadecimal UDP payload must contain complete byte pairs.");
        }

        return Convert.FromHexString(normalized);
    }
}
