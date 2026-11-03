using System.Globalization;
using CAN.Models;

namespace CAN.Helpers;

/// <summary>仅实现 P1/P2 的 CRC 输入与显式字段布局，不包含 E2E 接收监督状态机。</summary>
internal sealed record CanE2eDataId(CanCrcDataIdMode Mode, string Table, ushort Id,
    int CounterByte, int CounterBit, int NibbleByte, int NibbleBit, CanCrcDataIdPlacement Placement)
{
    internal bool IsProfile1 => Mode != CanCrcDataIdMode.Profile2;
    internal static CanE2eDataId? Capture(CanPayloadProtectionSetting s)
    {
        if (!s.CrcEnabled || !s.CrcDataIdEnabled) return null;
        if (!Enum.IsDefined(s.CrcDataIdMode)) throw new ArgumentException("CRC Data ID 模式无效");
        if (s.CrcDataIdMode == CanCrcDataIdMode.Fixed) return null;
        string table = "";
        ushort id = 0;
        if (s.CrcDataIdMode == CanCrcDataIdMode.Profile2)
        {
            if (s.CrcDataIdTable is not { Length: 16 }) throw new ArgumentException("Profile 2 Data ID 表必须恰好包含 16 项（Counter 0～15）");
            // Snapshot values, never retain an editor-owned mutable array.
            table = string.Concat(s.CrcDataIdTable.Select((entry, index) =>
                ParseNumber(entry, 0xFF, $"Profile 2 Data ID 表第 {index} 项（Counter={index}）").ToString("X2", CultureInfo.InvariantCulture)));
        }
        else
        {
            uint maximum = s.CrcDataIdMode switch
            {
                CanCrcDataIdMode.Profile1Low => 0xFF,
                CanCrcDataIdMode.Profile1Nibble => 0xFFF,
                _ => 0xFFFF
            };
            id = (ushort)ParseNumber(s.CrcProfile1DataIdHex, maximum, "Profile 1 Data ID");
        }
        bool nibble = s.CrcDataIdMode == CanCrcDataIdMode.Profile1Nibble;
        var placement = s.CrcDataIdUseProfilePlacement
            ? s.CrcDataIdMode == CanCrcDataIdMode.Profile2 ? CanCrcDataIdPlacement.AfterData : CanCrcDataIdPlacement.BeforeData
            : s.CrcDataIdPlacement;
        if (!Enum.IsDefined(placement)) throw new ArgumentException("自定义 Data ID 拼接位置无效");
        return new(s.CrcDataIdMode, table, id, s.CrcCounterByte, s.CrcCounterBitOffset,
            nibble ? s.CrcDataIdNibbleByte : 0, nibble ? s.CrcDataIdNibbleBitOffset : 0, placement);
    }

    private static uint ParseNumber(string? text, uint maximum, string name)
    {
        if (text == null || text.Length > 16) throw new ArgumentException($"{name} 必须为 0～{maximum:X} 的十六进制数");
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (text.Length == 0 || !uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint value) || value > maximum)
            throw new ArgumentException($"{name} 必须为 0～{maximum:X} 的十六进制数（可带 0x，不使用字节分隔符）");
        return value;
    }

    internal void Validate(int length, int crcStart, int crcEnd, int dataStart, int dataEnd, CanCrcFieldTreatment treatment)
    {
        ValidateNibble(CounterByte, CounterBit, length, "Counter");
        if (CounterByte >= crcStart && CounterByte <= crcEnd)
            throw new ArgumentException("Data ID 的 Counter 来源不能覆盖 CRC 输出字节");
        if (CounterByte < dataStart || CounterByte > dataEnd || treatment != CanCrcFieldTreatment.Skip)
            throw new ArgumentException("Profile 1/2 CRC 计算范围必须包含 Counter，并使用 Skip 排除 CRC 字段");
        if (Mode != CanCrcDataIdMode.Profile1Nibble) return;
        ValidateNibble(NibbleByte, NibbleBit, length, "Data ID NIBBLE");
        if (NibbleByte >= crcStart && NibbleByte <= crcEnd || NibbleByte == CounterByte && NibbleBit == CounterBit)
            throw new ArgumentException("Data ID NIBBLE 不能与 Counter 或 CRC 字段重叠");
        if (NibbleByte < dataStart || NibbleByte > dataEnd)
            throw new ArgumentException("CRC 计算范围必须包含显式 Data ID NIBBLE 字段");
    }

    private static void ValidateNibble(int at, int bit, int length, string name)
    {
        if (at < 0 || at >= length || bit is not (0 or 4))
            throw new ArgumentException($"{name} 必须位于实际数据内，半字节位偏移只能为 0（低四位）或 4（高四位）");
    }

    internal int Counter(ReadOnlySpan<byte> data) => (data[CounterByte] >> CounterBit) & 15;
    internal bool CounterValid(ReadOnlySpan<byte> data) => !IsProfile1 || Counter(data) != 15;
    internal bool? DataIdValid(ReadOnlySpan<byte> data) => Mode == CanCrcDataIdMode.Profile1Nibble
        ? ((data[NibbleByte] >> NibbleBit) & 15) == (Id >> 8) : null;

    internal void Prepare(Span<byte> data)
    {
        if (!CounterValid(data)) throw new ArgumentException("Profile 1 Counter 不能为 15；请将 RC 配置为 0～14 循环或提供有效的 Counter");
        if (Mode == CanCrcDataIdMode.Profile1Nibble)
            data[NibbleByte] = (byte)((data[NibbleByte] & ~(15 << NibbleBit)) | ((Id >> 8) << NibbleBit));
    }

    internal int CopyPrefix(ReadOnlySpan<byte> data, Span<byte> destination)
        => Placement == CanCrcDataIdPlacement.BeforeData ? CopySelectedId(data, destination) : 0;

    internal int CopySuffix(ReadOnlySpan<byte> data, Span<byte> destination)
        => Placement == CanCrcDataIdPlacement.AfterData ? CopySelectedId(data, destination) : 0;

    private int CopySelectedId(ReadOnlySpan<byte> data, Span<byte> destination)
    {
        if (!IsProfile1)
        {
            destination[0] = byte.Parse(Table.AsSpan(Counter(data) * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return 1;
        }
        destination[0] = (byte)Id;
        switch (Mode)
        {
            case CanCrcDataIdMode.Profile1Both:
                destination[1] = (byte)(Id >> 8); return 2;
            case CanCrcDataIdMode.Profile1Alt:
                if ((Counter(data) & 1) != 0) destination[0] = (byte)(Id >> 8);
                return 1;
            case CanCrcDataIdMode.Profile1Low: return 1;
            case CanCrcDataIdMode.Profile1Nibble:
                // PRS_E2E_00640 requires an implicit zero byte in addition to the low byte.
                destination[1] = 0; return 2;
            default: throw new ArgumentException("Profile 1 Data ID 模式无效");
        }
    }

}
