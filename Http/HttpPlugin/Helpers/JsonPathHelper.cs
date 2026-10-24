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
        return EvaluateNode(JsonNode.Parse(json), path);
    }

    public static string? EvaluateNode(JsonNode? root, string path)
    {
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
    private static IReadOnlyList<PathSegment> Tokenize(string path)
    {
        var segments = new List<PathSegment>();
        int position = 0;
        while (position < path.Length)
        {
            int begin = position;
            while (position < path.Length && path[position] is not ('.' or '[' or ']')) position++;
            if (position > begin) segments.Add(new PathSegment(path[begin..position], -1));
            else if (path[position] != '[' || (segments.Count > 0 && path[position - 1] == '.'))
                throw new FormatException("JSON 路径缺少属性名或包含不匹配的括号");

            while (position < path.Length && path[position] == '[')
            {
                int close = path.IndexOf(']', position + 1);
                if (close < 0) throw new FormatException("JSON 路径的数组索引缺少右括号");
                var indexText = path.AsSpan(position + 1, close - position - 1);
                if (indexText.IsEmpty || !int.TryParse(indexText, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out int index) || index < 0)
                    throw new FormatException("JSON 路径数组索引必须为非负整数");
                segments.Add(new PathSegment(string.Empty, index));
                position = close + 1;
            }
            if (position < path.Length)
            {
                if (path[position] != '.' || ++position == path.Length)
                    throw new FormatException("JSON 路径包含非法的索引后缀或末尾分隔符");
            }
        }
        return segments;
    }

    private readonly record struct PathSegment(string Name, int Index);
}
