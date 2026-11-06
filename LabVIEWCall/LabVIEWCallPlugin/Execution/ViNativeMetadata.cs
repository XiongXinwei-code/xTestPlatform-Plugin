using System.IO;
using System.Text;
using System.Text.Json;
using LabVIEWCallPlugin.LVadapter;
using Node = LabVIEWCallPlugin.Execution.LabVIEWCallExecutor.ViParameterNode;

namespace LabVIEWCallPlugin.Execution;

/// <summary>
/// Adapts only trusted native connector metadata to the editor/executor document format.
/// User settings must continue to go through the strict parameter document validator.
/// </summary>
internal static class ViNativeMetadata
{
    internal static ConnectPanelInfo Normalize(ConnectPanelInfo panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        // Do not partially update a panel if its other document is invalid.
        string inputs = NormalizeDocument(panel.ControlOut, input: true);
        string outputs = NormalizeDocument(panel.IndicatorOut, input: false);
        return new ConnectPanelInfo
        {
            ControlIn = panel.ControlIn,
            IndicatorIn = panel.IndicatorIn,
            ControlOut = inputs,
            IndicatorOut = outputs,
            IsReentrant = panel.IsReentrant,
            VIState = panel.VIState,
            Width = panel.Width,
            Height = panel.Height,
            PixelData = panel.PixelData
        };
    }

    private static string NormalizeDocument(string json, bool input)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        if (json.Length > 4 * 1024 * 1024)
            throw new InvalidDataException("VI 原生端子 JSON 超过 4 Mi 个字符。");
        try
        {
            using var document = JsonDocument.Parse(json);
            // A native Array owns one or more current element rows. Treating that
            // container as a Cluster permits the existing strict topology/field checks;
            // no field, duplicate property or orphan row is discarded before validation.
            string structural = Write(document.RootElement, structural: true);
            LabVIEWCallExecutor.ValidateParameterStructure(structural);

            var rows = document.RootElement.EnumerateArray().ToDictionary(
                row => Key(row[0]), row => row, StringComparer.Ordinal);
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            var removed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (key, row) in rows)
            {
                var data = row[1];
                if (Text(data, "Type") != "Array") continue;
                string location = Location(row[0]);
                if (!data.TryGetProperty("ChildNodePath", out var children) || children.GetArrayLength() == 0)
                    throw UnsupportedArray(location, "缺少标量元素定义，无法确定数组元素类型");
                string? arrayType = null;
                ViWireType? elementWire = null;
                foreach (var childPath in children.EnumerateArray())
                {
                    var childRow = rows[Key(childPath)];
                    var child = childRow[1];
                    string scalarName = Text(child, "Type");
                    Node scalar = new()
                    {
                        Path = childRow[0].EnumerateArray().Select(part => part.GetString()!).ToArray(),
                        Name = Text(child, "Name"), TypeName = scalarName,
                        Literal = "", Source = "Constant", Target = "", Missing = false, Log = false
                    };
                    ViWireType wire;
                    try { wire = ViWireCodec.ParseType(scalar); }
                    catch (InvalidDataException ex)
                    {
                        throw UnsupportedArray(location, $"元素类型 [{scalarName}] 不受支持", ex);
                    }
                    if (wire.IsArray || wire.IsCluster)
                        throw UnsupportedArray(location, $"元素类型 [{scalarName}] 不是简单标量");
                    if (elementWire.HasValue && elementWire.Value != wire)
                        throw UnsupportedArray(location, $"元素类型不一致，端子 [{Location(childPath)}] 的类型为 [{scalarName}]");
                    if (Text(child, "Variable").Length != 0 || Source(child) != "Constant" ||
                        Flag(child, "Log") || Flag(child, "isMissing"))
                        throw UnsupportedArray(location, "元素定义包含单独的变量、日志或缺失配置；请在数组父端子配置完整数组，不能丢弃元素配置");
                    elementWire = wire;
                    arrayType ??= scalarName.Trim() + "[]";
                    removed.Add(Key(childPath));
                }
                // After a run native metadata exposes every current element. Those
                // values may belong to the previous run; only the parent's explicit
                // complete value/binding is configuration, never rebuild it from children.
                replacements.Add(key, arrayType!);
            }
            string result = Write(document.RootElement, structural: false, replacements, removed, input);
            LabVIEWCallExecutor.ValidateParameterStructure(result);
            // This is a representational conversion, not proof of dimensionality.
            // ViWireCodec.CheckDescriptor still rejects non-1D native descriptors.
            return result;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"VI 原生端子 JSON 结构无效：{ex.Message}", ex);
        }
    }

    private static string Write(JsonElement root, bool structural,
        Dictionary<string, string>? replacements = null, HashSet<string>? removed = null, bool input = false)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            if (root.ValueKind != JsonValueKind.Array)
            {
                root.WriteTo(writer);
            }
            else
            {
                writer.WriteStartArray();
                foreach (var row in root.EnumerateArray())
                {
                    // Let the common validator report malformed rows and paths.
                    if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 2 ||
                        row[1].ValueKind != JsonValueKind.Object)
                    {
                        row.WriteTo(writer);
                        continue;
                    }
                    string? key = structural ? null : Key(row[0]);
                    if (key != null && removed?.Contains(key) == true) continue;
                    string? replacement = null;
                    bool array = key != null && replacements!.TryGetValue(key, out replacement);
                    bool emptyArray = array && input && Text(row[1], "Value").Length == 0 && Source(row[1]) == "Constant";
                    bool wroteValue = false;
                    writer.WriteStartArray();
                    row[0].WriteTo(writer);
                    writer.WriteStartObject();
                    foreach (var property in row[1].EnumerateObject())
                    {
                        writer.WritePropertyName(property.Name);
                        if (property.Name == "Type" && array)
                            writer.WriteStringValue(replacement);
                        else if (property.Name == "Type" && structural && property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() == "Array")
                            writer.WriteStringValue("Cluster");
                        else if (property.Name == "ChildNodePath" && array)
                        {
                            writer.WriteStartArray();
                            writer.WriteEndArray();
                        }
                        else if (property.Name == "ValueSourceType" && property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() == "")
                            writer.WriteStringValue("Constant");
                        else if (property.Name == "Value" && emptyArray)
                            writer.WriteStringValue("[]");
                        else property.Value.WriteTo(writer);
                        wroteValue |= property.Name == "Value";
                    }
                    if (emptyArray && !wroteValue) writer.WriteString("Value", "[]");
                    writer.WriteEndObject();
                    writer.WriteEndArray();
                }
                writer.WriteEndArray();
            }
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string Key(JsonElement path) => string.Join('\0', path.EnumerateArray().Select(part => part.GetString()));
    private static string Location(JsonElement path) => string.Join(" / ", path.EnumerateArray().Select(part => part.GetString()));
    private static string Text(JsonElement data, string name) => data.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
    private static string Source(JsonElement data) => Text(data, "ValueSourceType") is "" ? "Constant" : Text(data, "ValueSourceType");
    private static bool Flag(JsonElement data, string name) => data.TryGetProperty(name, out var value) && value.GetBoolean();
    private static InvalidDataException UnsupportedArray(string location, string reason, Exception? inner = null)
        => new($"VI 数组端子 [{location}]：{reason}；仅支持一维标量数组，暂不支持簇数组、引用数组或多维数组。", inner);
}
