using System.Text;

namespace Ethernet;

/// <summary>字节数组与字符串相互转换的编码帮助工具。</summary>
public static class EthernetDataHelper
{
    /// <summary>将字符串按指定编码格式转为字节数组。</summary>
    public static byte[] Encode(string data, EthernetDataEncoding encoding)
    {
        return encoding switch
        {
            EthernetDataEncoding.Hex   => ParseHex(data),
            EthernetDataEncoding.Utf8  => Encoding.UTF8.GetBytes(data),
            EthernetDataEncoding.Ascii => Encoding.ASCII.GetBytes(data),
            _                          => throw new ArgumentOutOfRangeException(nameof(encoding))
        };
    }

    /// <summary>将字节数组按指定编码格式转为字符串。</summary>
    public static string Decode(byte[] data, EthernetDataEncoding encoding)
    {
        return encoding switch
        {
            EthernetDataEncoding.Hex   => BitConverter.ToString(data).Replace("-", " "),
            EthernetDataEncoding.Utf8  => Encoding.UTF8.GetString(data),
            EthernetDataEncoding.Ascii => Encoding.ASCII.GetString(data),
            _                          => throw new ArgumentOutOfRangeException(nameof(encoding))
        };
    }

    private static byte[] ParseHex(string hex)
    {
        var tokens = hex.Split(new[] { ' ', '-', ',' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new byte[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
            result[i] = Convert.ToByte(tokens[i], 16);
        return result;
    }
}
