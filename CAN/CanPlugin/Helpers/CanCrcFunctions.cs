using CAN.Dbc;
using CAN.Models;

namespace CAN.Helpers;

/// <summary>离线 CRC 纯函数；不访问 CAN、表达式上下文或运行时 RC 状态。</summary>
public static class CanCrcFunctions
{
    /// <summary>
    /// 对已经选定的数据段末尾追加一个 Data ID 字节，计算 CRC-8/AUTOSAR
    /// （Poly=2F、Init=FF、XorOut=FF、无反射）。data 不应含要排除的 CRC 字节；
    /// 此函数不自动取 Counter、不查表，不修改输入，也不是完整 E2E 接收监督。
    /// </summary>
    public static byte E2E_Crc8(byte[] data, byte dataId)
    {
        ArgumentNullException.ThrowIfNull(data);
        var input = new byte[checked(data.Length + 1)];
        data.CopyTo(input, 0); input[^1] = dataId;
        return (byte)CanPayloadProtection.Calculate(input,
            new CrcParameters("CRC-8/AUTOSAR", 8, 0x2F, 0xFF, 0xFF, false, false));
    }

    /// <summary>
    /// 按配置从完整 CAN/FD 载荷选择 CRC 输入并返回数值。读取已有 Counter/NIBBLE，
    /// 不生成、不累加 RC，不写 CRC/NIBBLE，不验证接收监督状态。表表达式须由调用方先解析冻结。
    /// </summary>
    public static ulong Calculate(byte[] data, CanPayloadProtectionSetting protection)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(protection);
        if (data.Length > 64) throw new ArgumentException("完整 CAN/FD 载荷不能超过 64 字节", nameof(data));
        var setting = protection.Copy();
        if (!setting.CrcEnabled) throw new ArgumentException("离线 CRC 计算必须启用 CRC", nameof(protection));
        if (CanPayloadProtectionExpressions.UsesTableExpression(setting))
            throw new ArgumentException("纯函数不求值 Data ID 表表达式，请先解析并冻结为 16 项内联表", nameof(protection));
        var payload = data.ToArray();
        DbcPayload.ValidateReadProtection(setting, payload.Length);
        return CanPayloadProtection.Snapshot.Capture(setting).CalculateCrc(payload);
    }
}
