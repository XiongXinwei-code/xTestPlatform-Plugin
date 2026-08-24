using System.Globalization;
using System.Text;

namespace CAN.Flash.HexParser;

/// <summary>
/// Motorola S-Record 格式解析器。
/// 支持 S1/S2/S3 数据记录（地址宽度分别为 2/3/4 字节），
/// S0（头部）、S5/S6（记录计数）、S7/S8/S9（起始地址）在解析时忽略。
/// </summary>
public sealed class SRecordParser : IFirmwareParser
{
    public IReadOnlyList<FlashSegment> Parse(byte[] content, uint baseAddress)
    {
        var records = new List<(uint Address, byte[] Data)>();
        int lineNumber = 0;

        using var reader = new StringReader(Encoding.ASCII.GetString(content));
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            line = line.Trim();
            if (line.Length == 0)
                continue;

            if (line[0] != 'S' && line[0] != 's')
                throw new InvalidDataException($"S-Record 第 {lineNumber} 行未以 'S' 开头");

            if (line.Length < 4 || line.Length % 2 != 0)
                throw new InvalidDataException($"S-Record 第 {lineNumber} 行长度非法");

            char recordType = line[1];
            var raw = ParseHexBytes(line[2..], lineNumber);

            int byteCount = raw[0];
            if (raw.Length != byteCount + 1)
                throw new InvalidDataException(
                    $"S-Record 第 {lineNumber} 行字节数不匹配，声明 {byteCount} 字节，实际 {raw.Length - 1} 字节");

            byte sum = 0;
            for (int i = 0; i < raw.Length - 1; i++)
                sum += raw[i];
            if ((byte)(~sum) != raw[^1])
                throw new InvalidDataException($"S-Record 第 {lineNumber} 行校验和错误");

            int addressWidth = recordType switch
            {
                '1' => 2,
                '2' => 3,
                '3' => 4,
                _ => 0
            };

            if (addressWidth == 0)
            {
                // S0 头部、S5/S6 计数、S7/S8/S9 起始地址：烧录时不使用
                if (recordType is '0' or '4' or '5' or '6' or '7' or '8' or '9')
                    continue;

                throw new InvalidDataException(
                    $"S-Record 第 {lineNumber} 行存在不支持的记录类型 S{recordType}");
            }

            // 载荷长度 = 字节计数 - 地址宽度 - 校验和(1)
            int payloadLength = byteCount - addressWidth - 1;
            if (payloadLength < 0)
                throw new InvalidDataException($"S-Record 第 {lineNumber} 行数据长度非法");

            uint address = 0;
            for (int i = 0; i < addressWidth; i++)
                address = (address << 8) | raw[1 + i];

            var data = raw[(1 + addressWidth)..(1 + addressWidth + payloadLength)];
            records.Add((address, data));
        }

        if (records.Count == 0)
            throw new InvalidDataException("S-Record 文件中未找到任何数据记录 (S1/S2/S3)");

        return SegmentBuilder.Merge(records);
    }

    private static byte[] ParseHexBytes(string hex, int lineNumber)
    {
        var result = new byte[hex.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out result[i]))
                throw new InvalidDataException($"S-Record 第 {lineNumber} 行包含非十六进制字符");
        }
        return result;
    }
}
