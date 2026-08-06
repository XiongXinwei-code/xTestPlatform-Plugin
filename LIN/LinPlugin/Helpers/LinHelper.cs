namespace LIN.Helpers;

/// <summary>LIN 连接管理辅助类，通过 RuntimeData 存取适配器实例</summary>
public static class LinHelper
{
    private const string KeyPrefix = "LIN_Adapter_";

    public static string GetAdapterKey(string connectionName) => $"{KeyPrefix}{connectionName}";

    /// <summary>解析 LIN 帧 ID 字符串（支持 0x 前缀十六进制或十进制，范围 0-63）</summary>
    public static byte ParseFrameId(string idStr)
    {
        idStr = idStr.Trim();
        byte id;
        if (idStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            id = Convert.ToByte(idStr[2..], 16);
        else
            id = byte.Parse(idStr);

        if (id > 63)
            throw new ArgumentOutOfRangeException(nameof(idStr), $"LIN 帧 ID 必须在 0-63 范围内，当前值: {id}");
        return id;
    }

    /// <summary>计算 LIN 保护 ID（加入奇偶校验位）</summary>
    public static byte CalcProtectedId(byte frameId)
    {
        // LIN 保护 ID: P0 = ID0^ID1^ID2^ID4, P1 = ~(ID1^ID3^ID4^ID5)
        int id = frameId & 0x3F;
        int p0 = ((id >> 0) ^ (id >> 1) ^ (id >> 2) ^ (id >> 4)) & 1;
        int p1 = (~((id >> 1) ^ (id >> 3) ^ (id >> 4) ^ (id >> 5))) & 1;
        return (byte)(id | (p0 << 6) | (p1 << 7));
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
