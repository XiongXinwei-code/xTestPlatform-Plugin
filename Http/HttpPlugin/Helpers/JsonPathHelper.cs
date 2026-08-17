using System.Text.Json.Nodes;

namespace Http.Helpers;

/// <summary>
/// 简化版 JSON 路径解析器，支持点号属性访问与方括号数组索引，例如 data.items[0].sn
/// </summary>
internal static class JsonPathHelper
{
    /// <summary>
    /// 按路径从 JSON 文本中取值。
    /// 路径为空时返回根节点文本；节点不存在时返回 null。
    /// </summary>
    public static string? Evaluate(string json, string path)
    {
        var root = JsonNode.Parse(json);
        if (root == null) return null;

        var node = string.IsNullOrWhiteSpace(path) ? root : Navigate(root, path);
        if (node == null) return null;

        // 标量直接取原始值，避免字符串被额外加上引号
        return node is JsonValue value ? value.ToString() : node.ToJsonString();
    }

    private static JsonNode? Navigate(JsonNode root, string path)
    {
        var current = root;

        foreach (var segment in Tokenize(path))
        {
            if (current == null) return null;

            if (segment.Index >= 0)
            {
                if (current is not JsonArray array || segment.Index >= array.Count) return null;
                current = array[segment.Index];
                continue;
            }

            if (current is not JsonObject obj || !obj.TryGetPropertyValue(segment.Name, out var child)) return null;
            current = child;
        }

        return current;
    }

    /// <summary>将 a.b[0].c 拆解为属性名与数组索引的有序序列</summary>
    private static IEnumerable<PathSegment> Tokenize(string path)
    {
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = part;

            var bracket = name.IndexOf('[');
            if (bracket < 0)
            {
                yield return new PathSegment(name, -1);
                continue;
            }

            var propertyName = name[..bracket];
            if (propertyName.Length > 0)
                yield return new PathSegment(propertyName, -1);

            // 同一段可能带有多级索引，例如 matrix[0][1]
            var rest = name[bracket..];
            while (rest.StartsWith('['))
            {
                var close = rest.IndexOf(']');
                if (close < 0) yield break;

                if (!int.TryParse(rest[1..close], out var index)) yield break;
                yield return new PathSegment(string.Empty, index);

                rest = rest[(close + 1)..];
            }
        }
    }

    private readonly record struct PathSegment(string Name, int Index);
}
