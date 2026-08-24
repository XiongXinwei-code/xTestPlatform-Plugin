using System.Globalization;
using System.Text;

namespace CAN.Flash.HexParser;

/// <summary>
/// Intel HEX 格式解析器。
/// 支持记录类型 00（数据）、01（文件结束）、02（扩展段地址）、04（扩展线性地址）、
/// 03/05（起始地址，解析时忽略）。
/// </summary>
public sealed class IntelHexParser : IFirmwareParser
{
    public IReadOnlyList<FlashSegment> Parse(byte[] content, uint baseAddress)
    {
        var records = new List<(uint Address, byte[] Data)>();
        uint addressBase = 0;
        bool endReached = false;
        int lineNumber = 0;

        using var reader = new StringReader(Encoding.ASCII.GetString(content));
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            line = line.Trim();
            if (line.Length == 0)
                continue;

            if (line[0] != ':')
                throw new InvalidDataException($"Intel HEX 第 {lineNumber} 行未以 ':' 开头");

            if (line.Length < 11 || (line.Length - 1) % 2 != 0)
                throw new InvalidDataException($"Intel HEX 第 {lineNumber} 行长度非法");

            var raw = ParseHexBytes(line[1..], lineNumber);

            int byteCount = raw[0];
            if (raw.Length != byteCount + 5)
                throw new InvalidDataException(
                    $"Intel HEX 第 {lineNumber} 行字节数不匹配，声明 {byteCount} 字节，实际 {raw.Length - 5} 字节");

            byte sum = 0;
            foreach (var b in raw)
                sum += b;
            if (sum != 0)
                throw new InvalidDataException($"Intel HEX 第 {lineNumber} 行校验和错误");

            ushort offset = (ushort)((raw[1] << 8) | raw[2]);
            byte recordType = raw[3];
            var data = raw[4..(4 + byteCount)];

            switch (recordType)
            {
                case 0x00: // 数据记录
                    records.Add((addressBase + offset, data));
                    break;

                case 0x01: // 文件结束
                    endReached = true;
                    break;

                case 0x02: // 扩展段地址
                    if (byteCount != 2)
                        throw new InvalidDataException($"Intel HEX 第 {lineNumber} 行扩展段地址记录长度必须为 2 字节");
                    addressBase = (uint)((data[0] << 8) | data[1]) << 4;
                    break;

                case 0x04: // 扩展线性地址
                    if (byteCount != 2)
                        throw new InvalidDataException($"Intel HEX 第 {lineNumber} 行扩展线性地址记录长度必须为 2 字节");
                    addressBase = (uint)((data[0] << 8) | data[1]) << 16;
                    break;

                case 0x03: // 起始段地址，烧录时不使用
                case 0x05: // 起始线性地址，烧录时不使用
                    break;

                default:
                    throw new InvalidDataException(
                        $"Intel HEX 第 {lineNumber} 行存在不支持的记录类型 0x{recordType:X2}");
            }

            if (endReached)
                break;
        }

        if (!endReached)
            throw new InvalidDataException("Intel HEX 文件缺少文件结束记录 (:00000001FF)");

        if (records.Count == 0)
            throw new InvalidDataException("Intel HEX 文件中未找到任何数据记录");

        return SegmentBuilder.Merge(records);
    }

    private static byte[] ParseHexBytes(string hex, int lineNumber)
    {
        var result = new byte[hex.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out result[i]))
                throw new InvalidDataException($"Intel HEX 第 {lineNumber} 行包含非十六进制字符");
        }
        return result;
    }
}
