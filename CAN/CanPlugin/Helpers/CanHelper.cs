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
        if ((hexStr.Length & 1) != 0)
            throw new FormatException("CAN 数据必须由完整的十六进制字节组成，不能包含单独的半字节");
        var bytes = new byte[hexStr.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hexStr.Substring(i * 2, 2), 16);
        return bytes;
    }

    /// <summary>统一发送边界，不允许厂商驱动通过截断 ID 或载荷隐式改变报文。</summary>
    internal static void ValidateTransmitFrame(CanMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        uint maximumId = message.FrameType switch
        {
            CanFrameType.Standard => 0x7FF,
            CanFrameType.Extended => 0x1FFFFFFF,
            _ => throw new ArgumentException("CAN 帧类型无效", nameof(message))
        };
        if (message.Id > maximumId)
            throw new ArgumentException($"CAN ID 0x{message.Id:X} 超出{(message.FrameType == CanFrameType.Standard ? "标准帧 11 位" : "扩展帧 29 位")}范围", nameof(message));
        if (message.Data == null || message.Data.Length > (message.IsFd ? 64 : 8))
            throw new ArgumentException($"CAN {(message.IsFd ? "FD" : "Classic")} 载荷不得为空引用或超过 {(message.IsFd ? 64 : 8)} 字节；不允许静默截断", nameof(message));
    }
}
