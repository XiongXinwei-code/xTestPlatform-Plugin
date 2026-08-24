using CAN.Flash.Models;

namespace CAN.Flash.HexParser;

/// <summary>
/// 固件解析器工厂，按显式格式或文件扩展名选择解析器。
/// </summary>
public static class FirmwareParserFactory
{
    public static IFirmwareParser Create(FirmwareFormat format, string filePath)
    {
        var actual = format == FirmwareFormat.Auto ? Detect(filePath) : format;

        return actual switch
        {
            FirmwareFormat.IntelHex => new IntelHexParser(),
            FirmwareFormat.SRecord => new SRecordParser(),
            FirmwareFormat.Binary => new BinParser(),
            _ => throw new NotSupportedException($"不支持的固件格式: {actual}")
        };
    }

    /// <summary>根据文件扩展名推断固件格式</summary>
    public static FirmwareFormat Detect(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".hex" or ".ihex" or ".ihx" => FirmwareFormat.IntelHex,
            ".s19" or ".srec" or ".s28" or ".s37" or ".mot" or ".sre" => FirmwareFormat.SRecord,
            ".bin" or ".raw" => FirmwareFormat.Binary,
            _ => throw new NotSupportedException(
                $"无法根据扩展名 \"{ext}\" 识别固件格式，请在编辑器中显式指定固件格式")
        };
    }
}
