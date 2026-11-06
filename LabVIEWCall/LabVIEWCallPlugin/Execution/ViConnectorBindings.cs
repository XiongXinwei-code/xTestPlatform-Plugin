using System.IO;
using System.Text;
using System.Text.Json;
using LabVIEWCallPlugin.LVadapter;

namespace LabVIEWCallPlugin.Execution;

/// <summary>Preserves editor bindings only when the complete live connector identity still matches.</summary>
internal static class ViConnectorBindings
{
    private static readonly HashSet<string> BindingFields = new(StringComparer.Ordinal)
        { "Value", "Variable", "ValueSourceType", "Log" };

    internal static ConnectPanelInfo Preserve(ConnectPanelInfo normalizedLive, string savedInputs, string savedOutputs)
    {
        ArgumentNullException.ThrowIfNull(normalizedLive);
        // Validate both saved documents before processing either direction. In particular,
        // never deserialize through a permissive model that silently drops unknown fields.
        LabVIEWCallExecutor.ValidateParameterStructure(savedInputs);
        LabVIEWCallExecutor.ValidateParameterStructure(savedOutputs);
        LabVIEWCallExecutor.ValidateParameterStructure(normalizedLive.ControlOut);
        LabVIEWCallExecutor.ValidateParameterStructure(normalizedLive.IndicatorOut);

        string inputs = Merge(normalizedLive.ControlOut, savedInputs, "输入");
        string outputs = Merge(normalizedLive.IndicatorOut, savedOutputs, "输出");
        // Publish a new complete panel only after both directions have succeeded.
        return new ConnectPanelInfo
        {
            ControlIn = normalizedLive.ControlIn,
            IndicatorIn = normalizedLive.IndicatorIn,
            ControlOut = inputs,
            IndicatorOut = outputs,
            IsReentrant = normalizedLive.IsReentrant,
            VIState = normalizedLive.VIState,
            Width = normalizedLive.Width,
            Height = normalizedLive.Height,
            PixelData = normalizedLive.PixelData
        };
    }

    private static string Merge(string live, string saved, string direction)
    {
        if (string.IsNullOrWhiteSpace(saved)) return live;
        using var savedDocument = JsonDocument.Parse(saved);
        if (savedDocument.RootElement.GetArrayLength() == 0) return live;
        using var liveDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(live) ? "[]" : live);
        var liveRows = liveDocument.RootElement.EnumerateArray().ToDictionary(
            row => Key(row[0]), row => row, StringComparer.Ordinal);
        var savedRows = savedDocument.RootElement.EnumerateArray().ToDictionary(
            row => Key(row[0]), row => row, StringComparer.Ordinal);
        foreach (var (key, row) in savedRows)
        {
            if (!liveRows.TryGetValue(key, out var current))
                throw Mismatch(direction, row[0], "当前 VI 中缺少此路径，端子可能已移除或移动");
            var previousData = row[1];
            var currentData = current[1];
            if (Text(previousData, "Name") != Text(currentData, "Name"))
                throw Mismatch(direction, row[0], "端子名称已变化");
            if (Text(previousData, "Type") != Text(currentData, "Type"))
                throw Mismatch(direction, row[0], $"端子类型已由 [{Text(previousData, "Type")}] 变为 [{Text(currentData, "Type")}]");
            if (!Children(previousData).SequenceEqual(Children(currentData), StringComparer.Ordinal))
                throw Mismatch(direction, row[0], "ChildNodePath 子端子结构或顺序已变化");
            if (currentData.TryGetProperty("isMissing", out var missing) && missing.GetBoolean())
                throw Mismatch(direction, row[0], "当前 VI 将此端子标记为缺失");
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var row in liveDocument.RootElement.EnumerateArray())
            {
                if (!savedRows.TryGetValue(Key(row[0]), out var previous))
                {
                    row.WriteTo(writer);
                    continue;
                }
                writer.WriteStartArray();
                row[0].WriteTo(writer);
                writer.WriteStartObject();
                foreach (var property in row[1].EnumerateObject())
                    if (!BindingFields.Contains(property.Name)) property.WriteTo(writer);
                var data = previous[1];
                writer.WriteString("Value", Text(data, "Value"));
                writer.WriteString("Variable", Text(data, "Variable"));
                string source = Text(data, "ValueSourceType");
                writer.WriteString("ValueSourceType", source.Length == 0 ? "Constant" : source);
                writer.WriteBoolean("Log", data.TryGetProperty("Log", out var log) && log.GetBoolean());
                writer.WriteEndObject();
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }
        string result = Encoding.UTF8.GetString(buffer.ToArray());
        LabVIEWCallExecutor.ValidateParameterStructure(result);
        return result;
    }

    private static string Key(JsonElement path) => string.Join('\0', path.EnumerateArray().Select(part => part.GetString()));
    private static string Text(JsonElement data, string name) => data.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
    private static IEnumerable<string> Children(JsonElement data) => data.TryGetProperty("ChildNodePath", out var children)
        ? children.EnumerateArray().Select(Key) : [];
    private static InvalidDataException Mismatch(string direction, JsonElement path, string reason)
        => new($"VI {direction}端子 [{string.Join(" / ", path.EnumerateArray().Select(part => part.GetString()))}]：{reason}。"
            + "未应用本次刷新，已保留原配置；请先核对并清除失效映射，再刷新端子。");
}
