using System.IO;
using CAN.Flash.HexParser;
using CAN.Flash.Models;

namespace CAN.UI.Services;

/// <summary>固件分析结果</summary>
public sealed class FirmwareAnalysisResult
{
    /// <summary>分析是否成功</summary>
    public bool Success { get; init; }

    /// <summary>失败原因或提示信息（中文）</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>实际识别到的固件格式</summary>
    public FirmwareFormat DetectedFormat { get; init; }

    /// <summary>数据段数量</summary>
    public int SegmentCount { get; init; }

    /// <summary>数据总字节数</summary>
    public int TotalBytes { get; init; }

    /// <summary>最小起始地址</summary>
    public uint MinAddress { get; init; }

    /// <summary>最大结束地址（含）</summary>
    public uint MaxAddress { get; init; }

    /// <summary>推导出的地址与长度格式标识建议值，形如 "0x44"</summary>
    public string SuggestedAddressAndLengthFormatId { get; init; } = string.Empty;

    /// <summary>各数据段的明细描述</summary>
    public IReadOnlyList<string> SegmentDetails { get; init; } = Array.Empty<string>();
}

/// <summary>
/// 编辑期固件分析服务，复用执行层的解析器读取固件结构，
/// 用于在编辑器中展示数据段概况并推导格式标识建议值。
/// </summary>
public static class FirmwareAnalyzer
{
    private const int MaxSegmentDetails = 20;

    /// <summary>
    /// 分析固件文件。<paramref name="filePathExpression"/> 为表达式字段原文，
    /// 仅支持形如 "D:\\fw.hex" 的字面量字符串，含变量引用时无法在编辑期解析。
    /// </summary>
    public static async Task<FirmwareAnalysisResult> AnalyzeAsync(
        string filePathExpression, FirmwareFormat format, string baseAddressExpression,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePathExpression))
            return Fail("请先选择固件文件");

        if (!TryExtractLiteral(filePathExpression, out var filePath))
            return Fail("固件路径包含变量或表达式，无法在编辑期解析，请改用固定路径后再分析");

        if (!File.Exists(filePath))
            return Fail($"固件文件不存在: {filePath}");

        try
        {
            var actualFormat = format == FirmwareFormat.Auto
                ? FirmwareParserFactory.Detect(filePath)
                : format;

            uint baseAddress = 0;
            if (actualFormat == FirmwareFormat.Binary)
            {
                if (!TryExtractLiteral(baseAddressExpression, out var baseText) ||
                    !TryParseAddress(baseText, out baseAddress))
                    return Fail("二进制格式需要一个固定的基地址才能分析，请填写形如 0x08000000 的字面量");
            }

            var content = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var parser = FirmwareParserFactory.Create(actualFormat, filePath);
            var segments = parser.Parse(content, baseAddress);

            if (segments.Count == 0)
                return Fail("固件文件解析结果为空，没有可烧录的数据");

            uint minAddress = segments.Min(s => s.StartAddress);
            uint maxAddress = segments.Max(s => s.EndAddress);
            int maxLength = segments.Max(s => s.Length);
            int totalBytes = segments.Sum(s => s.Length);

            int addressBytes = RequiredBytes(maxAddress);
            int lengthBytes = RequiredBytes((uint)maxLength);
            byte alfid = (byte)((lengthBytes << 4) | addressBytes);

            var details = segments
                .Take(MaxSegmentDetails)
                .Select(s => $"0x{s.StartAddress:X8} - 0x{s.EndAddress:X8}    {s.Length:N0} 字节")
                .ToList();

            if (segments.Count > MaxSegmentDetails)
                details.Add($"... 其余 {segments.Count - MaxSegmentDetails} 个数据段未列出");

            return new FirmwareAnalysisResult
            {
                Success = true,
                Message = $"解析成功：{actualFormat}，共 {segments.Count} 个数据段，合计 {totalBytes:N0} 字节",
                DetectedFormat = actualFormat,
                SegmentCount = segments.Count,
                TotalBytes = totalBytes,
                MinAddress = minAddress,
                MaxAddress = maxAddress,
                SuggestedAddressAndLengthFormatId = $"0x{alfid:X2}",
                SegmentDetails = details
            };
        }
        catch (OperationCanceledException)
        {
            return Fail("分析已取消");
        }
        catch (Exception ex)
        {
            return Fail($"解析失败: {ex.Message}");
        }
    }

    /// <summary>表示某个数值最少需要几个字节承载（至少 1，最多 4）</summary>
    private static int RequiredBytes(uint value)
    {
        if (value <= 0xFF) return 1;
        if (value <= 0xFFFF) return 2;
        if (value <= 0xFFFFFF) return 3;
        return 4;
    }

    /// <summary>
    /// 从表达式原文中提取字面量字符串。表达式字段中的字符串以引号包裹，
    /// 只有整体是一个被引号包裹的字面量时才认为可在编辑期取值。
    /// </summary>
    private static bool TryExtractLiteral(string expression, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var text = expression.Trim();

        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            var inner = text[1..^1];
            if (inner.Contains('"'))
                return false;

            value = inner.Replace("\\\\", "\\");
            return true;
        }

        // 未加引号但本身就是纯数字或十六进制字面量（数值型表达式字段）
        if (text.All(c => char.IsLetterOrDigit(c) || c is 'x' or 'X'))
        {
            value = text;
            return true;
        }

        return false;
    }

    private static bool TryParseAddress(string text, out uint value)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out value);
        return uint.TryParse(text, out value);
    }

    private static FirmwareAnalysisResult Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
