namespace CAN.Flash.HexParser;

/// <summary>
/// 原始二进制固件解析器。二进制文件不含地址信息，
/// 整个文件作为单个数据段，起始地址由用户配置的基地址决定。
/// </summary>
public sealed class BinParser : IFirmwareParser
{
    public IReadOnlyList<FlashSegment> Parse(byte[] content, uint baseAddress)
    {
        if (content.Length == 0)
            throw new InvalidDataException("二进制固件文件内容为空");

        return
        [
            new FlashSegment
            {
                StartAddress = baseAddress,
                Data = content
            }
        ];
    }
}
