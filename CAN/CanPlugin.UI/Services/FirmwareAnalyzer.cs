using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;
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

    internal static string FilePathExpression(string filePath) => JsonSerializer.Serialize(filePath);

    /// <summary>
    /// 分析固件文件。<paramref name="filePathExpression"/> 为表达式字段原文，
    /// 仅支持形如 "D:\\fw.hex" 的字面量字符串，含变量引用时无法在编辑期解析。
    /// </summary>
    public static Task<FirmwareAnalysisResult> AnalyzeAsync(
        string filePathExpression, FirmwareFormat format, string baseAddressExpression,
        CancellationToken cancellationToken = default)
        => AnalyzeAsync(filePathExpression, format, baseAddressExpression, cancellationToken, FirmwareParserFactory.Create);

    internal static Task<FirmwareAnalysisResult> AnalyzeAsync(
        string filePathExpression, FirmwareFormat format, string baseAddressExpression,
        CancellationToken cancellationToken,
        Func<FirmwareFormat, string, IFirmwareParser> createParser)
        => AnalyzeAsync(filePathExpression, format, baseAddressExpression, cancellationToken, createParser, File.Exists);

    internal static Task<FirmwareAnalysisResult> AnalyzeAsync(
        string filePathExpression, FirmwareFormat format, string baseAddressExpression,
        CancellationToken cancellationToken,
        Func<FirmwareFormat, string, IFirmwareParser> createParser, Func<string, bool> fileExists)
        // Exists/open/read can all block on a network drive, before an awaited
        // read yields. Keep their real task alive even after its caller cancels.
        => Task.Run(() => AnalyzeFileAsync(filePathExpression, format, baseAddressExpression,
            cancellationToken, createParser, fileExists), CancellationToken.None);

    private static async Task<FirmwareAnalysisResult> AnalyzeFileAsync(
        string filePathExpression, FirmwareFormat format, string baseAddressExpression,
        CancellationToken cancellationToken,
        Func<FirmwareFormat, string, IFirmwareParser> createParser, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(filePathExpression))
            return Fail("请先选择固件文件");

        if (!TryExtractLiteral(filePathExpression, out var filePath))
            return Fail("固件路径包含变量或表达式，无法在编辑期解析，请改用固定路径后再分析");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!fileExists(filePath))
                return Fail($"固件文件不存在: {filePath}");
            cancellationToken.ThrowIfCancellationRequested();
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

            var content = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            var parser = createParser(actualFormat, filePath);
            var segments = parser.Parse(content, baseAddress);
            cancellationToken.ThrowIfCancellationRequested();

            if (segments.Count == 0)
                return Fail("固件文件解析结果为空，没有可烧录的数据");

            uint minAddress = segments.Min(s => s.StartAddress);
            if (segments.Any(s => s.Length <= 0 || (ulong)s.StartAddress + (uint)s.Length > 0x1_0000_0000UL))
                return Fail("固件数据段为空或越过32位地址空间");
            uint maxAddress = segments.Max(s => checked((uint)((ulong)s.StartAddress + (uint)s.Length - 1)));
            int maxLength = segments.Max(s => s.Length);
            int totalBytes = segments.Sum(s => s.Length);

            int addressBytes = RequiredBytes(maxAddress);
            int lengthBytes = RequiredBytes((uint)maxLength);
            byte alfid = (byte)((lengthBytes << 4) | addressBytes);

            var details = segments
                .Take(MaxSegmentDetails)
                .Select(s => $"0x{s.StartAddress:X8} - 0x{(ulong)s.StartAddress + (uint)s.Length - 1:X8}    {s.Length:N0} 字节")
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

        if (text.StartsWith("@\"", StringComparison.Ordinal) && text.EndsWith('"'))
        {
            var literal = new StringBuilder();
            for (int i = 2; i < text.Length - 1; i++)
            {
                if (text[i] == '"' && (i + 1 >= text.Length - 1 || text[++i] != '"')) return false;
                literal.Append(text[i]);
            }
            value = literal.ToString();
            return true;
        }

        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            // Decode literals only; never run an arbitrary edit-time expression.
            var literal = new StringBuilder();
            for (int i = 1; i < text.Length - 1; i++)
            {
                char c = text[i];
                if (c == '"' || c is '\r' or '\n') return false;
                if (c != '\\') { literal.Append(c); continue; }
                if (++i >= text.Length - 1) return false;
                c = text[i];
                string? escaped = c switch
                {
                    '\\' => "\\", '"' => "\"", '\'' => "'", '0' => "\0", 'a' => "\a",
                    'b' => "\b", 'f' => "\f", 'n' => "\n", 'r' => "\r", 't' => "\t", 'v' => "\v", _ => null
                };
                if (escaped != null) { literal.Append(escaped); continue; }
                if (c is not ('u' or 'U' or 'x')) return false;
                int minimum = c == 'x' ? 1 : c == 'u' ? 4 : 8;
                int maximum = c == 'U' ? 8 : 4;
                int length = 0;
                while (length < maximum && i + 1 + length < text.Length - 1 && Uri.IsHexDigit(text[i + 1 + length])) length++;
                if (length < minimum || !uint.TryParse(text.AsSpan(i + 1, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint code)) return false;
                if (c == 'U')
                {
                    if (code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF) return false;
                    literal.Append(char.ConvertFromUtf32((int)code));
                }
                else literal.Append((char)code);
                i += length;
            }
            value = literal.ToString();
            return true;
        }

        // Numeric/hex base-address literals only, not an unquoted variable name.
        if (TryParseAddress(text, out _))
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
            return uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static FirmwareAnalysisResult Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
