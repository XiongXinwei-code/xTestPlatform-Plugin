using System.Buffers.Binary;
using System.Globalization;

namespace CiA402.Core;

public static class ObjectCodec
{
    public static long Decode(byte[] data, int bytes, bool signed)
    {
        if (data.Length != bytes) throw new InvalidOperationException($"对象数据长度错误：期望 {bytes} 字节，实际 {data.Length} 字节");
        return (bytes, signed) switch
        {
            (1, false) => data[0], (1, true) => unchecked((sbyte)data[0]),
            (2, false) => BinaryPrimitives.ReadUInt16LittleEndian(data), (2, true) => BinaryPrimitives.ReadInt16LittleEndian(data),
            (4, false) => BinaryPrimitives.ReadUInt32LittleEndian(data), (4, true) => BinaryPrimitives.ReadInt32LittleEndian(data),
            _ => throw new ArgumentException("仅支持 1、2、4 字节对象")
        };
    }

    public static byte[] Encode(long value, int bytes, bool signed)
    {
        long min = signed ? -(1L << (bytes * 8 - 1)) : 0;
        long max = signed ? (1L << (bytes * 8 - 1)) - 1 : (1L << (bytes * 8)) - 1;
        if (bytes is not (1 or 2 or 4) || value < min || value > max) throw new ArgumentOutOfRangeException(nameof(value), "对象写入值超出范围");
        var result = new byte[bytes];
        for (var i = 0; i < bytes; i++) result[i] = (byte)(value >> (8 * i));
        return result;
    }

    public static int ParseNumber(string text) => text.Trim().StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        ? int.Parse(text.Trim()[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
        : int.Parse(text.Trim(), CultureInfo.InvariantCulture);
}

public sealed record PdoEntry(ushort Index, byte SubIndex, int Bits)
{
    public uint Descriptor => ((uint)Index << 16) | ((uint)SubIndex << 8) | (uint)Bits;
}

public static class PdoLayout
{
    public static IReadOnlyList<PdoEntry> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("PDO 布局不能为空");
        var items = text.Split(',').Select(part =>
        {
            var fields = part.Trim().Split(':');
            if (fields.Length != 3) throw new ArgumentException("PDO 布局必须为 索引:子索引:位数，使用逗号分隔");
            var index = ObjectCodec.ParseNumber(fields[0]);
            var sub = ObjectCodec.ParseNumber(fields[1]);
            var bits = ObjectCodec.ParseNumber(fields[2]);
            if (index is < 0 or > 65535 || sub is < 0 or > 255 || bits is not (8 or 16 or 32)) throw new ArgumentException("PDO 映射索引、子索引或位数无效");
            return new PdoEntry((ushort)index, (byte)sub, bits);
        }).ToArray();
        if (items.Sum(x => x.Bits) > 64 || items.Select(x => (x.Index, x.SubIndex)).Distinct().Count() != items.Length)
            throw new ArgumentException("经典 CAN PDO 总长度不得超过 64 位，映射项不得重复");
        return items;
    }
}
