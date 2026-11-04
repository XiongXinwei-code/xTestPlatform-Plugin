using System.Globalization;
using System.Text;
using CAN.Models;

namespace CAN.Helpers;

/// <summary>标准正向多项式（不含最高次项），数值和字节写入顺序互相独立。</summary>
public sealed record CrcParameters(string Name, int Width, ulong Polynomial, ulong InitialValue,
    ulong XorOutput, bool ReflectInput, bool ReflectOutput);

public static class CanPayloadProtection
{
    public static CrcParameters GetParameters(CanPayloadProtectionSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        if (setting.CrcEnabled && setting.CrcDataIdEnabled && setting.CrcDataIdMode != CanCrcDataIdMode.Fixed)
        {
            if (!Enum.IsDefined(setting.CrcDataIdMode)) throw new ArgumentException("CRC Data ID 模式无效");
            string suffix = setting.CrcDataIdUseProfilePlacement ? "" : "（自定义 Data ID 拼接）";
            return setting.CrcDataIdMode == CanCrcDataIdMode.Profile2
                ? new("CRC-8/AUTOSAR (Profile 2)" + suffix, 8, 0x2F, 0xFF, 0xFF, false, false)
                : new("AUTOSAR E2E Profile 1 CRC" + suffix, 8, 0x1D, 0, 0, false, false);
        }
        // Standard names and check values: https://reveng.sourceforge.io/crc-catalogue/all.htm
        return setting.CrcAlgorithm switch
        {
            CanCrcAlgorithm.Crc8 => new("CRC-8/SMBUS", 8, 0x07, 0, 0, false, false),
            CanCrcAlgorithm.Crc8SaeJ1850 => new("CRC-8/SAE-J1850", 8, 0x1D, 0xFF, 0xFF, false, false),
            CanCrcAlgorithm.Crc8Autosar => new("CRC-8/AUTOSAR", 8, 0x2F, 0xFF, 0xFF, false, false),
            CanCrcAlgorithm.Crc16Modbus => new("CRC-16/MODBUS", 16, 0x8005, 0xFFFF, 0, true, true),
            CanCrcAlgorithm.Crc16CcittFalse => new("CRC-16/IBM-3740 (CCITT-FALSE)", 16, 0x1021, 0xFFFF, 0, false, false),
            CanCrcAlgorithm.Crc32IsoHdlc => new("CRC-32/ISO-HDLC", 32, 0x04C11DB7, 0xFFFFFFFF, 0xFFFFFFFF, true, true),
            CanCrcAlgorithm.Crc8MaximDow => new("CRC-8/MAXIM-DOW", 8, 0x31, 0, 0, true, true),
            CanCrcAlgorithm.Crc8Rohc => new("CRC-8/ROHC", 8, 0x07, 0xFF, 0, true, true),
            CanCrcAlgorithm.Crc16Arc => new("CRC-16/ARC", 16, 0x8005, 0, 0, true, true),
            CanCrcAlgorithm.Crc16X25 => new("CRC-16/IBM-SDLC (X-25)", 16, 0x1021, 0xFFFF, 0xFFFF, true, true),
            CanCrcAlgorithm.Crc16Xmodem => new("CRC-16/XMODEM", 16, 0x1021, 0, 0, false, false),
            CanCrcAlgorithm.Crc16Kermit => new("CRC-16/KERMIT", 16, 0x1021, 0, 0, true, true),
            CanCrcAlgorithm.Crc16Usb => new("CRC-16/USB", 16, 0x8005, 0xFFFF, 0xFFFF, true, true),
            CanCrcAlgorithm.Crc16Dnp => new("CRC-16/DNP", 16, 0x3D65, 0, 0xFFFF, true, true),
            CanCrcAlgorithm.Crc32C => new("CRC-32/ISCSI (CRC-32C)", 32, 0x1EDC6F41, 0xFFFFFFFF, 0xFFFFFFFF, true, true),
            CanCrcAlgorithm.Crc32Mpeg2 => new("CRC-32/MPEG-2", 32, 0x04C11DB7, 0xFFFFFFFF, 0, false, false),
            CanCrcAlgorithm.Crc32Bzip2 => new("CRC-32/BZIP2", 32, 0x04C11DB7, 0xFFFFFFFF, 0xFFFFFFFF, false, false),
            CanCrcAlgorithm.Crc32Jamcrc => new("CRC-32/JAMCRC", 32, 0x04C11DB7, 0xFFFFFFFF, 0, true, true),
            CanCrcAlgorithm.Crc64Ecma182 => new("CRC-64/ECMA-182", 64, 0x42F0E1EBA9EA3693, 0, 0, false, false),
            CanCrcAlgorithm.Crc64Xz => new("CRC-64/XZ", 64, 0x42F0E1EBA9EA3693, ulong.MaxValue, ulong.MaxValue, true, true),
            CanCrcAlgorithm.Custom => CustomParameters(setting),
            _ => throw new ArgumentException("CRC 算法无效")
        };
    }

    public static void Validate(CanPayloadProtectionSetting? setting, int payloadLength)
        => Snapshot.Capture(setting).Validate(payloadLength);

    public static void ValidateConfiguration(CanPayloadProtectionSetting? setting, int maxLength)
        => Snapshot.Capture(setting).Validate(maxLength);

    /// <summary>DBC 和编辑页共用精确位覆盖；bit 为字节内 LSB 编号的绝对位。</summary>
    public static bool ControlsBit(CanPayloadProtectionSetting s, int bit)
    {
        int at = bit / 8, offset = bit % 8;
        return s.RcEnabled && at >= s.RcStartByte && at <= s.RcEndByte &&
                (!s.RcUseBitField || offset >= s.RcBitOffset && offset < s.RcBitOffset + s.RcBitLength)
            || s.CrcEnabled && at >= s.CrcStartByte && at <= s.CrcEndByte
            || s.CrcEnabled && s.CrcDataIdEnabled && s.CrcDataIdMode == CanCrcDataIdMode.Profile1Nibble &&
                at == s.CrcDataIdNibbleByte && offset >= s.CrcDataIdNibbleBitOffset && offset < s.CrcDataIdNibbleBitOffset + 4;
    }

    private static CrcParameters CustomParameters(CanPayloadProtectionSetting setting)
    {
        if (setting.CrcWidth is not (8 or 16 or 32 or 64))
            throw new ArgumentException("自定义 CRC WIDTH 必须为 8、16、32 或 64");
        ulong mask = Mask(setting.CrcWidth);
        ulong polynomial = ParseHex(setting.CrcPolynomial, "POLY", mask);
        if (polynomial == 0 || (polynomial & 1) == 0)
            throw new ArgumentException("CRC POLY 必须为非零奇数，使用不含最高次项的标准正向十六进制多项式");
        return new(setting.CrcName ?? "Custom", setting.CrcWidth, polynomial,
            ParseHex(setting.CrcInitialValue, "INIT", mask), ParseHex(setting.CrcXorOutput, "XOROUT", mask),
            setting.CrcReflectInput, setting.CrcReflectOutput);
    }

    private static ulong ParseHex(string? text, string name, ulong mask)
    {
        text = text?.Trim();
        if (text?.StartsWith("0x", StringComparison.OrdinalIgnoreCase) == true) text = text[2..];
        if (string.IsNullOrEmpty(text) || !ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value) || value > mask)
            throw new ArgumentException($"CRC {name} 必须是 WIDTH 范围内的十六进制数（可省略 0x 前缀）");
        return value;
    }

    private static ulong Mask(int width) => width == 64 ? ulong.MaxValue : (1UL << width) - 1;

    private static string NormalizeDataId(string? text)
    {
        const string error = "CRC Data ID 必须为 1～64 个完整十六进制字节，按输入顺序计算（可用空白、逗号、短横线分隔，整体可带 0x 前缀）";
        if (text == null || text.Length > 1024) throw new ArgumentException(error);
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..].Trim();
        if (text.Length == 0 || text[0] is '-' or ',' || text[^1] is '-' or ',') throw new ArgumentException(error);
        var normalized = new StringBuilder(128);
        int tokenLength = 0;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c) || c is '-' or ',')
            {
                // Do not silently combine two half bytes such as "1 2".
                if ((tokenLength & 1) != 0) throw new ArgumentException(error);
                tokenLength = 0;
                continue;
            }
            if (!Uri.IsHexDigit(c) || normalized.Length >= 128) throw new ArgumentException(error);
            normalized.Append(char.ToUpperInvariant(c));
            tokenLength++;
        }
        if (normalized.Length == 0 || (tokenLength & 1) != 0) throw new ArgumentException(error);
        return normalized.ToString();
    }

    internal static ulong Calculate(ReadOnlySpan<byte> data, CrcParameters p)
    {
        ulong value = p.InitialValue, mask = Mask(p.Width), top = 1UL << (p.Width - 1);
        foreach (byte item in data)
        {
            ulong input = p.ReflectInput ? Reflect(item, 8) : item;
            value ^= input << (p.Width - 8);
            for (int bit = 0; bit < 8; bit++) value = ((value & top) != 0 ? (value << 1) ^ p.Polynomial : value << 1) & mask;
        }
        if (p.ReflectOutput) value = Reflect(value, p.Width);
        return (value ^ p.XorOutput) & mask;
    }

    private static ulong Reflect(ulong value, int bits)
    {
        ulong result = 0;
        for (int bit = 0; bit < bits; bit++) { result = (result << 1) | (value & 1); value >>= 1; }
        return result;
    }

    internal sealed record Snapshot(bool RcEnabled, int RcStart, int RcEnd, CanPayloadByteOrder RcOrder,
        uint RcInitial, uint RcMin, uint RcMax, long RcStep, CanCounterOverflow RcOverflow,
        bool CrcEnabled, int CrcStart, int CrcEnd, CanPayloadByteOrder CrcOrder,
        int DataStart, int DataEnd, CanCrcFieldTreatment CrcTreatment, CrcParameters? Crc,
        string DataIdHex, CanCrcDataIdPlacement DataIdPlacement,
        bool RcUseBits, int RcBitOffset, int RcBitLength, CanE2eDataId? Profile)
    {
        internal static Snapshot Capture(CanPayloadProtectionSetting? setting)
        {
            var s = setting ?? new();
            if (CanPayloadProtectionExpressions.UsesTableExpression(s))
                throw new ArgumentException("Data ID 表表达式尚未解析，必须在操作准备阶段求值并冻结，不能使用内联表代替");
            var profile = CanE2eDataId.Capture(s);
            bool useDataId = s.CrcEnabled && s.CrcDataIdEnabled && profile == null;
            if (useDataId && !Enum.IsDefined(s.CrcDataIdPlacement))
                throw new ArgumentException("CRC Data ID 计算位置无效，必须选择 BeforeData 或 AfterData");
            return new(s.RcEnabled, s.RcStartByte, s.RcEndByte, s.RcByteOrder,
                s.RcInitialValue, s.RcMinimum, s.RcMaximum, s.RcStep, s.RcOverflow,
                s.CrcEnabled, s.CrcStartByte, s.CrcEndByte, s.CrcByteOrder,
                s.CrcDataStartByte, s.CrcDataEndByte, s.CrcFieldTreatment, s.CrcEnabled ? GetParameters(s) : null,
                useDataId ? NormalizeDataId(s.CrcDataIdHex) : "",
                useDataId ? s.CrcDataIdPlacement : CanCrcDataIdPlacement.BeforeData,
                s.RcEnabled && s.RcUseBitField, s.RcEnabled && s.RcUseBitField ? s.RcBitOffset : 0,
                s.RcEnabled && s.RcUseBitField ? s.RcBitLength : 0, profile);
        }

        internal void Validate(int length)
        {
            if (length is < 0 or > 64) throw new ArgumentException("载荷长度必须为 0～64 字节");
            if (RcEnabled)
            {
                ValidateRange(RcStart, RcEnd, length, "RC 写入");
                int bytes = RcEnd - RcStart + 1;
                if (bytes > 4) throw new ArgumentException("RC 写入范围必须占用 1～4 个完整字节");
                if (!Enum.IsDefined(RcOrder) || !Enum.IsDefined(RcOverflow)) throw new ArgumentException("RC 字节序或溢出规则无效");
                if (RcUseBits && (bytes != 1 || RcBitOffset < 0 || RcBitLength is < 1 or > 8 || RcBitOffset > 8 - RcBitLength))
                    throw new ArgumentException("RC 位域必须在单一字节内，位偏移从 0 开始，位宽为 1～8 且不得跨字节");
                ulong maximum = (1UL << (RcUseBits ? RcBitLength : bytes * 8)) - 1;
                if (RcMin > RcMax || RcMax > maximum || RcInitial < RcMin || RcInitial > RcMax)
                    throw new ArgumentException("RC 上下限或初值无效，必须落在写入字节可表示的范围内");
                if (RcStep < -(long)uint.MaxValue || RcStep > uint.MaxValue)
                    throw new ArgumentException("RC 步长必须在 -4294967295～4294967295 之间");
            }
            if (!CrcEnabled) return;
            ValidateRange(CrcStart, CrcEnd, length, "CRC 写入");
            if (CrcEnd - CrcStart + 1 != Crc!.Width / 8) throw new ArgumentException($"CRC 写入范围必须恰好占用 {Crc.Width / 8} 字节（WIDTH={Crc.Width}）");
            if (!Enum.IsDefined(CrcOrder) || !Enum.IsDefined(CrcTreatment)) throw new ArgumentException("CRC 字节序或字段参与方式无效");
            if (RcEnabled && RcStart <= CrcEnd && CrcStart <= RcEnd) throw new ArgumentException("RC 与 CRC 写入范围不能重叠");
            int end = DataEnd == -1 ? length - 1 : DataEnd;
            ValidateRange(DataStart, end, length, "CRC 计算");
            Profile?.Validate(length, CrcStart, CrcEnd, DataStart, end, CrcTreatment);
            if (Profile != null && RcEnabled && (!RcUseBits || RcBitLength != 4 || RcStart != Profile.CounterByte ||
                RcEnd != Profile.CounterByte || RcBitOffset != Profile.CounterBit || RcMax > (Profile.IsProfile1 ? 14U : 15U)))
                throw new ArgumentException("Profile 1/2 的 RC 必须为 Counter 来源处的四位位域，P1 上限不超过 14，P2 不超过 15");
            if (CrcTreatment == CanCrcFieldTreatment.Skip && DataStart >= CrcStart && end <= CrcEnd && DataIdHex.Length == 0 && Profile == null)
                throw new ArgumentException("排除 CRC 字段后，参与 CRC 计算的数据不能为空");
        }

        // Shared by transmit generation and DBC receive verification. The ID is
        // implicit CRC input, not a field in the wire payload or a separate CRC.
        internal ulong CalculateCrc(ReadOnlySpan<byte> payload)
        {
            Span<byte> input = stackalloc byte[128]; // CAN FD 64 + optional Data ID 64.
            int count = 0;
            if (Profile != null) count += Profile.CopyPrefix(payload, input);
            if (DataIdPlacement == CanCrcDataIdPlacement.BeforeData) count += CopyDataId(input);
            int end = DataEnd == -1 ? payload.Length - 1 : DataEnd;
            for (int index = DataStart; index <= end; index++)
            {
                bool crcByte = index >= CrcStart && index <= CrcEnd;
                if (crcByte && CrcTreatment == CanCrcFieldTreatment.Skip) continue;
                input[count++] = crcByte ? (byte)0 : payload[index];
            }
            if (DataIdPlacement == CanCrcDataIdPlacement.AfterData) count += CopyDataId(input[count..]);
            if (Profile != null) count += Profile.CopySuffix(payload, input[count..]);
            return Calculate(input[..count], Crc!);
        }

        private int CopyDataId(Span<byte> destination)
        {
            for (int i = 0; i < DataIdHex.Length / 2; i++)
                destination[i] = byte.Parse(DataIdHex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return DataIdHex.Length / 2;
        }

        private static void ValidateRange(int start, int end, int length, string name)
        {
            if (start < 0 || end < start || end >= length)
                throw new ArgumentException($"{name}起止字节必须满足 0 ≤ 起始 ≤ 结束 < 数据长度 {length}（包含结束字节）");
        }
    }
}

/// <summary>单一发送源的不可变配置快照与 RC 状态。调用方必须在同一发送串行门内 Prepare / CommitAttempt / 提交。</summary>
public sealed class CanPayloadProcessor
{
    private readonly CanPayloadProtection.Snapshot _setting;
    private uint _next;
    private bool _exhausted;
    private bool _prepared;

    public CanPayloadProcessor(CanPayloadProtectionSetting? setting)
    {
        _setting = CanPayloadProtection.Snapshot.Capture(setting);
        _setting.Validate(64);
        _next = _setting.RcInitial;
    }

    public bool Matches(CanPayloadProtectionSetting? setting)
        => _setting == CanPayloadProtection.Snapshot.Capture(setting);

    // The connection send pool calls this while holding its admission boundary.
    // Reconfiguration keeps the next counter, including an exhausted Error state.
    internal CanPayloadProcessor Reconfigure(CanPayloadProtectionSetting? setting)
    {
        var replacement = new CanPayloadProcessor(setting);
        if (_setting.RcEnabled && replacement._setting.RcEnabled)
        {
            if (_next < replacement._setting.RcMin || _next > replacement._setting.RcMax)
                throw new ArgumentException($"新 RC 范围不能表示发送池下一计数 {_next}，旧配置保持不变");
            replacement._next = _next;
            replacement._exhausted = _exhausted;
        }
        return replacement;
    }

    /// <summary>只在副本上先写 RC 再计算 CRC；无发送或提交失败时，不改变调用方的数据。</summary>
    public byte[] Prepare(byte[] payload)
    {
        _prepared = false;
        ArgumentNullException.ThrowIfNull(payload);
        _setting.Validate(payload.Length);
        if (_exhausted) throw new InvalidOperationException("RC 已超出配置上下限（Error 规则），本次未提交；请重新启动发送源或更改配置");
        byte[] copy = (byte[])payload.Clone();
        if (_setting.RcEnabled)
        {
            if (_setting.RcUseBits)
            {
                int mask = ((1 << _setting.RcBitLength) - 1) << _setting.RcBitOffset;
                copy[_setting.RcStart] = (byte)((copy[_setting.RcStart] & ~mask) | ((int)_next << _setting.RcBitOffset));
            }
            else WriteValue(copy, _setting.RcStart, _setting.RcEnd, _next, _setting.RcOrder);
        }
        _setting.Profile?.Prepare(copy);
        if (_setting.CrcEnabled)
        {
            ulong crc = _setting.CalculateCrc(copy);
            WriteValue(copy, _setting.CrcStart, _setting.CrcEnd, crc, _setting.CrcOrder);
        }
        _prepared = true;
        return copy;
    }

    /// <summary>紧接原生提交前消耗计数；原生提交抛错或软等待超时也不回滚，避免未知提交重用 RC。</summary>
    public void CommitAttempt()
    {
        if (!_prepared) throw new InvalidOperationException("必须先成功准备当前载荷，再提交 RC 计数");
        _prepared = false;
        if (!_setting.RcEnabled) return;
        long candidate = (long)_next + _setting.RcStep;
        if (candidate >= _setting.RcMin && candidate <= _setting.RcMax) { _next = (uint)candidate; return; }
        switch (_setting.RcOverflow)
        {
            case CanCounterOverflow.Wrap:
                long range = (long)_setting.RcMax - _setting.RcMin + 1;
                long offset = (candidate - _setting.RcMin) % range;
                if (offset < 0) offset += range;
                _next = (uint)(_setting.RcMin + offset);
                break;
            case CanCounterOverflow.Hold:
                _next = (uint)Math.Clamp(candidate, _setting.RcMin, _setting.RcMax);
                break;
            case CanCounterOverflow.Error:
                _exhausted = true;
                break;
        }
    }

    private static void WriteValue(byte[] data, int start, int end, ulong value, CanPayloadByteOrder order)
    {
        int width = end - start + 1;
        for (int offset = 0; offset < width; offset++)
            data[start + offset] = (byte)(value >> (8 * (order == CanPayloadByteOrder.LittleEndian ? offset : width - 1 - offset)));
    }
}
