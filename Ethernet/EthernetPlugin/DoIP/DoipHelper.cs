using Ethernet.DoIP.Models;

namespace Ethernet.DoIP;

/// <summary>DoIP 执行器公共辅助方法。</summary>
internal static class DoipHelper
{
    /// <summary>解析逻辑地址字符串（支持 0x 前缀十六进制与十进制）。</summary>
    public static ushort ParseAddress(string addrStr)
    {
        addrStr = addrStr.Trim();
        if (addrStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt16(addrStr[2..], 16);
        return ushort.Parse(addrStr);
    }

    /// <summary>解析空格分隔的十六进制数据串。</summary>
    public static byte[] ParseHexData(string hexStr)
    {
        var parts = hexStr.Trim().Split(new[] { ' ', '-', ',' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Select(p => Convert.ToByte(p, 16)).ToArray();
    }

    /// <summary>字节数组转十六进制字符串。</summary>
    public static string ToHex(byte[] data) => BitConverter.ToString(data).Replace("-", " ");

    /// <summary>激活类型枚举转协议字节。</summary>
    public static byte ToActivationByte(DoipActivationType type) => type switch
    {
        DoipActivationType.Default         => 0x00,
        DoipActivationType.WwhObd          => 0x01,
        DoipActivationType.CentralSecurity => 0xE0,
        _                                  => 0x00
    };
}
