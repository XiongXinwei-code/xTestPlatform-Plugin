using CAN.Models;

namespace CAN.Helpers;

/// <summary>CAN 连接管理辅助类，通过资源注册表存取适配器实例</summary>
public static class CanHelper
{
    private const string KeyPrefix = "CAN_Adapter_";

    public static string GetAdapterKey(string connectionName) => $"{KeyPrefix}{connectionName}";

    /// <summary>解析 CAN ID 字符串（支持 0x 前缀十六进制或十进制）</summary>
    public static uint ParseCanId(string idStr)
    {
        idStr = idStr.Trim();
        if (idStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt32(idStr[2..], 16);
        return uint.Parse(idStr);
    }

    /// <summary>解析十六进制数据字符串为字节数组</summary>
    public static byte[] ParseHexData(string hexStr)
    {
        if (string.IsNullOrWhiteSpace(hexStr)) return [];
        hexStr = hexStr.Replace("-", "").Replace(" ", "").Replace(",", "");
        var bytes = new byte[hexStr.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hexStr.Substring(i * 2, 2), 16);
        return bytes;
    }
}
