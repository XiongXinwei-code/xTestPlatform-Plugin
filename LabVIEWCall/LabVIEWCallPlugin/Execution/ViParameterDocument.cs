using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LabVIEWCallPlugin.Execution;

public sealed partial class LabVIEWCallExecutor
{
    internal sealed class ViParameterNode
    {
        internal required string Name { get; init; }
        internal required string TypeName { get; init; }
        internal required string[] Path { get; init; }
        internal required string Literal { get; init; }
        internal required string Source { get; init; }
        internal required string Target { get; init; }
        internal required bool Missing { get; init; }
        internal required bool Log { get; init; }
        internal ViWireType Wire { get; set; }
        internal object? Value { get; set; }
        internal List<ViParameterNode> Children { get; } = [];
        internal string Location => string.Join(" / ", Path);
        internal InvalidDataException Error(string reason, Exception? inner = null)
            => new($"VI 端子 [{Location}]（{TypeName}）：{reason}", inner);
    }

    private const int MaxNodes = 4096;
    private const int MaxDepth = 48;
    private static readonly HashSet<string> KnownFields = new(StringComparer.Ordinal)
    { "Index", "Name", "Tag", "Value", "Type", "isMissing", "Log", "IconIndex", "ValueSourceType", "Variable", "ChildNodePath" };
    private static readonly Regex WritablePath = new(
        @"\A(?:Locals|FileGlobals|ProjectGlobals|Globals|Parameters)(?:\.[\p{L}_][\p{L}\p{M}\p{Nd}_]*)+\z", RegexOptions.CultureInvariant);

    public static void ValidateParameterStructure(string json) => ReadStructure(json);

    public static void ValidateParameterDocument(string json, bool isOutput) => ParseParameterNodes(json, isOutput);

    internal static List<ViParameterNode> ParseParameterNodes(string json, bool isOutput = false)
    {
        var roots = ReadStructure(json);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in roots) ValidateNode(node, isOutput, false, targets);
        // '.' sorts before every admitted identifier character, so a parent and its
        // first mapped descendant immediately follow one another in ordinal order.
        string? previous = null;
        foreach (string path in targets.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (previous != null && path.Length > previous.Length && path[previous.Length] == '.'
                && path.StartsWith(previous, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"VI 输出变量 [{previous}] 和 [{path}] 重叠，请只绑定整个变量或分别绑定其字段。");
            previous = path;
        }
        return roots;
    }

    private static List<ViParameterNode> ReadStructure(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        if (json.Length > 4 * 1024 * 1024) throw new InvalidDataException("VI 参数 JSON 超过 4 Mi 个字符。");
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() > MaxNodes)
                throw new InvalidDataException($"VI 参数必须是最多 {MaxNodes} 个端子的数组。");
            var nodes = new Dictionary<string, ViParameterNode>(StringComparer.Ordinal);
            var links = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in root.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 2 || row[1].ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("VI 参数行格式应为 [路径数组, 端子对象]。");
                var path = ReadPath(row[0]);
                string key = PathKey(path);
                var data = row[1];
                var fields = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in data.EnumerateObject())
                {
                    if (!KnownFields.Contains(property.Name) || !fields.Add(property.Name))
                        throw new InvalidDataException($"端子 [{key}] 的字段 [{property.Name}] 未定义或重复。");
                    if (property.Name is "Index" or "IconIndex") _ = property.Value.GetInt32();
                    if (property.Name == "Tag") _ = property.Value.GetString();
                }
                var node = new ViParameterNode
                {
                    Path = path,
                    Name = ReadText(data, "Name"),
                    TypeName = ReadText(data, "Type"),
                    Literal = ReadText(data, "Value"),
                    Target = ReadText(data, "Variable"),
                    Source = ReadText(data, "ValueSourceType", "Constant"),
                    Missing = data.TryGetProperty("isMissing", out var missing) && missing.GetBoolean(),
                    Log = data.TryGetProperty("Log", out var log) && log.GetBoolean()
                };
                if (string.IsNullOrWhiteSpace(node.Name) || node.Name.Contains('\0') || string.IsNullOrWhiteSpace(node.TypeName))
                    throw node.Error("必须填写有效的 Name 和 Type。");
                if (!nodes.TryAdd(key, node)) throw node.Error("路径重复。");
                if (!data.TryGetProperty("ChildNodePath", out var children)) continue;
                if (children.ValueKind != JsonValueKind.Array || children.GetArrayLength() > MaxNodes)
                    throw node.Error("ChildNodePath 必须是有界路径列表。");
                if (children.GetArrayLength() > 0 && node.TypeName != "Cluster")
                    throw node.Error("只有 Cluster 可包含子端子。");
                var childKeys = new List<string>();
                foreach (var child in children.EnumerateArray())
                {
                    var childPath = ReadPath(child);
                    if (childPath.Length != path.Length + 1 || !childPath.AsSpan(0, path.Length).SequenceEqual(path))
                        throw node.Error("子节点路径必须在父路径末尾追加一段。");
                    string childKey = PathKey(childPath);
                    if (!referenced.Add(childKey)) throw node.Error("子节点被多次引用。");
                    childKeys.Add(childKey);
                }
                links.Add(key, childKeys.ToArray());
            }
            foreach (var (key, children) in links)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var childKey in children)
                {
                    if (!nodes.TryGetValue(childKey, out var child)) throw nodes[key].Error($"缺少子端子 [{childKey}]。");
                    if (!names.Add(child.Name)) throw nodes[key].Error($"子字段名 [{child.Name}] 重复。");
                    nodes[key].Children.Add(child);
                }
            }
            var roots = new List<ViParameterNode>();
            var rootNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (key, node) in nodes)
            {
                if (referenced.Contains(key)) continue;
                if (node.Path.Length != 1) throw node.Error("节点与父端子未关联。");
                if (!rootNames.Add(node.Name)) throw node.Error("顶层端子名称重复。");
                roots.Add(node);
            }
            return roots;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"VI 端子 JSON 结构无效：{ex.Message}", ex);
        }
    }

    private static string ReadText(JsonElement obj, string property, string fallback = "")
        => obj.TryGetProperty(property, out var item) ? item.GetString() ?? fallback : fallback;

    private static string[] ReadPath(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() is < 1 or > MaxDepth)
            throw new InvalidDataException($"VI 端子路径深度应为 1 至 {MaxDepth}。");
        return value.EnumerateArray().Select(segment =>
        {
            string? text = segment.GetString();
            if (string.IsNullOrWhiteSpace(text) || text.Contains('\0'))
                throw new InvalidDataException("VI 端子路径段无效。");
            return text;
        }).ToArray();
    }

    private static string PathKey(string[] path) => string.Join('\0', path);

    private static void ValidateNode(ViParameterNode node, bool output, bool inherited, HashSet<string> targets)
    {
        node.Wire = ViWireCodec.ParseType(node);
        if (node.Missing) throw node.Error("已不在当前 VI 的面板中。");
        if (node.Log) throw node.Error("此桥未支持逐端子 Log，请关闭该选项。");
        if (node.Source is not ("Constant" or "Variable")) throw node.Error($"不支持来源 [{node.Source}]。");
        if (output)
        {
            if (node.Target.Length != 0 && (!WritablePath.IsMatch(node.Target) || !targets.Add(node.Target)))
                throw node.Error($"输出变量 [{node.Target}] 必须是明确且不重复的完整可写变量路径。");
        }
        else if (node.Source == "Variable")
        {
            if (inherited || string.IsNullOrWhiteSpace(node.Target)) throw node.Error("输入表达式为空或与上级整簇绑定冲突。");
        }
        else if (!inherited && !node.Wire.IsCluster)
            node.Value = ViWireCodec.ConvertValue(node, node.Literal, literal: true);
        foreach (var child in node.Children) ValidateNode(child, output, inherited || node.Source == "Variable", targets);
    }

    internal static void ValidateOutputTargets(List<ViParameterNode> nodes, IExecutionContext context)
    {
        foreach (var node in nodes)
        {
            if (node.Target.Length != 0 && !context.HasVariable(node.Target))
                throw node.Error($"输出变量 [{node.Target}] 未声明。");
            ValidateOutputTargets(node.Children, context);
        }
    }

    internal static async Task ResolveInputVariablesAsync(List<ViParameterNode> nodes, IExecutionContext context, Action checkActive)
    {
        foreach (var node in nodes)
        {
            checkActive();
            if (node.Source == "Variable")
            {
                object? value;
                try
                {
                    value = await ExpressionEvaluatorFactory.Default.EvaluateAsync<object>(node.Target, context)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw node.Error($"输入表达式 [{node.Target}] 求值失败。", ex);
                }
                checkActive();
                if (value is null) throw node.Error($"输入表达式 [{node.Target}] 返回空。");
                if (node.Wire.IsCluster) ViWireCodec.BindCluster(node, value);
                else node.Value = ViWireCodec.ConvertValue(node, value);
            }
            else await ResolveInputVariablesAsync(node.Children, context, checkActive).ConfigureAwait(false);
        }
    }
}
