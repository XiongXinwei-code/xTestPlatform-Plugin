namespace CAN.Flash.HexParser;

/// <summary>
/// 固件文件解析器契约，将固件文本/二进制内容解析为地址连续的数据段集合。
/// </summary>
public interface IFirmwareParser
{
    /// <summary>
    /// 解析固件文件内容。
    /// </summary>
    /// <param name="content">固件文件原始字节</param>
    /// <param name="baseAddress">基地址，仅原始二进制格式使用</param>
    /// <returns>按起始地址升序排列且已合并的数据段</returns>
    IReadOnlyList<FlashSegment> Parse(byte[] content, uint baseAddress);
}
