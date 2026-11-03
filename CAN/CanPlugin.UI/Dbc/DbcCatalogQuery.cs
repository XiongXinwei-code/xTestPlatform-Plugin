using System.Globalization;
using System.Text.Json;
using CAN.Dbc;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UI.Dbc;

/// <summary>Design-time, paged discovery against the same parsed catalog used for validation.</summary>
internal static class DbcCatalogQuery
{
    private const int PageSize = 8;

    internal static StepSettingError Describe(DbcDatabase database, string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query.Length > 160) return Invalid("查询过长");
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is 1 or 2 && words[0].Equals("messages", StringComparison.OrdinalIgnoreCase))
        {
            return Page(database.Messages, words.ElementAtOrDefault(1), "messages", page =>
                database.Messages.OrderBy(m => m.Id).ThenBy(m => m.Extended).Skip((page - 1) * PageSize).Take(PageSize)
                    .Select(m => new { messageId = m.Id, idHex = $"0x{m.Id:X}", frameType = m.Extended ? "Extended" : "Standard",
                        name = m.Name, byteLength = m.Length, isFd = m.IsFd, signalCount = m.Signals.Count,
                        cycleTimeMs = m.CycleTimeMs, cycleTimeSource = m.CycleTimeSource }).ToArray());
        }
        if (words.Length is not (3 or 4) || !words[0].Equals("signals", StringComparison.OrdinalIgnoreCase))
            return Invalid("不认识的查询");
        bool extended = words[1].Equals("Extended", StringComparison.OrdinalIgnoreCase);
        if (!extended && !words[1].Equals("Standard", StringComparison.OrdinalIgnoreCase))
            return Invalid("帧类型须为 Standard 或 Extended");
        if (!words[2].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            !uint.TryParse(words[2].AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var id) ||
            id > (extended ? 0x1FFFFFFFu : 0x7FFu))
            return Invalid("请输入与帧类型匹配的 0x 十六进制 ID");
        DbcMessage message;
        try { message = database.Find(id, extended); }
        catch (KeyNotFoundException ex) { return Invalid(ex.Message); }
        var command = $"signals {(extended ? "Extended" : "Standard")} 0x{id:X}";
        return Page(message.Signals, words.ElementAtOrDefault(3), command, page =>
            message.Signals.Skip((page - 1) * PageSize).Take(PageSize).Select(s => new {
                name = s.Name, startBit = s.StartBit, bitLength = s.BitLength, byteOrder = s.IsLittleEndian ? "Intel" : "Motorola",
                signed = s.IsSigned, factor = s.Factor, offset = s.Offset, minimum = s.Minimum, maximum = s.Maximum, unit = s.Unit,
                initialRawValue = s.HasInitialValue ? (decimal?)s.InitialRawValue : null,
                isMultiplexer = s.IsMultiplexer, multiplexerValue = s.MultiplexerValue
            }).ToArray());
    }

    private static StepSettingError Page<T>(IReadOnlyList<T> source, string? pageText, string command, Func<int, object> rows)
    {
        int page = 1;
        int pageCount = Math.Max(1, (source.Count + PageSize - 1) / PageSize);
        if (pageText != null && (!int.TryParse(pageText, NumberStyles.None, CultureInfo.InvariantCulture, out page) ||
            page < 1 || page > pageCount))
            return Invalid($"页号必须在 1～{pageCount} 之间，共 {source.Count} 项");
        string result = JsonSerializer.Serialize(new { query = $"{command} {page}", page, pageCount, total = source.Count,
            nextQuery = page < pageCount ? $"{command} {page + 1}" : null, items = rows(page) });
        return StepSettingError.Info("DBC_CATALOG", result);
    }

    private static StepSettingError Invalid(string reason) => StepSettingError.Warning("DBC_CATALOG_QUERY",
        $"目录查询无效：{reason}。用法：messages [页号] 或 signals Standard|Extended 0xID [页号]。此查询仅用于设计时查看目录。");
}
