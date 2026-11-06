using System.Buffers.Binary;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using LabVIEWCallPlugin.LVadapter.Interop;
using Node = LabVIEWCallPlugin.Execution.LabVIEWCallExecutor.ViParameterNode;

namespace LabVIEWCallPlugin.Execution;

internal readonly record struct ViWireType(LVTypeDescCode Scalar, bool IsArray = false)
{
    public bool IsCluster => Scalar == LVTypeDescCode.Cluster;
}

internal static class ViWireCodec
{
    private const int MaxBytes = 16 * 1024 * 1024;
    private const int MaxElements = 1_000_000;
    // Existing plugin payload strings are UTF-8. Reject binary or other text encodings explicitly.
    private static readonly UTF8Encoding TextEncoding = new(false, true);
    private static readonly Dictionary<string, LVTypeDescCode> ScalarNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["I8"] = LVTypeDescCode.Int8,
        ["I16"] = LVTypeDescCode.Int16,
        ["I32"] = LVTypeDescCode.Int32,
        ["I64"] = LVTypeDescCode.Int64,
        ["U8"] = LVTypeDescCode.UInt8,
        ["U16"] = LVTypeDescCode.UInt16,
        ["U32"] = LVTypeDescCode.UInt32,
        ["U64"] = LVTypeDescCode.UInt64,
        ["Single Float"] = LVTypeDescCode.Float32,
        ["Double Float"] = LVTypeDescCode.Float64,
        ["SGL"] = LVTypeDescCode.Float32,
        ["DBL"] = LVTypeDescCode.Float64,
        ["Boolean"] = LVTypeDescCode.Boolean,
        ["String"] = LVTypeDescCode.String,
        ["Cluster"] = LVTypeDescCode.Cluster,
        ["Int32"] = LVTypeDescCode.Int32,
        ["Int64"] = LVTypeDescCode.Int64,
        ["Float64"] = LVTypeDescCode.Float64,
        ["Byte"] = LVTypeDescCode.UInt8
    };

    internal static bool IsScalar(string typeName, LVTypeDescCode expected)
        => ScalarNames.TryGetValue(typeName.Trim(), out var actual) && actual == expected;

    internal static ViWireType ParseType(Node node)
    {
        var name = node.TypeName.Trim();
        bool array = true;
        if (name.StartsWith("1D Array of ", StringComparison.OrdinalIgnoreCase)) name = name[12..];
        else if (name.StartsWith("Array of ", StringComparison.OrdinalIgnoreCase)) name = name[9..];
        else if (name.EndsWith(" Array", StringComparison.OrdinalIgnoreCase)) name = name[..^6];
        else if (name.EndsWith("[]", StringComparison.Ordinal)) name = name[..^2];
        else array = false;
        if (!ScalarNames.TryGetValue(name, out var scalar) || array && scalar == LVTypeDescCode.Cluster)
            throw node.Error("不支持此 LabVIEW 类型；可使用普通标量、一维标量数组或簇端子。");
        return new(scalar, array);
    }

    internal static void BindCluster(Node node, object value)
    {
        IEnumerable<KeyValuePair<string, object?>> fields = value switch
        {
            JsonElement { ValueKind: JsonValueKind.Object } obj => obj.EnumerateObject()
                .Select(p => new KeyValuePair<string, object?>(p.Name, p.Value)),
            string json when json.Length <= MaxBytes => ReadClusterFields(node, json),
            IEnumerable<KeyValuePair<string, object?>> map => map,
            IDictionary dictionary => ReadDictionaryFields(node, dictionary),
            _ => throw node.Error("整簇输入必须是带命名字段的结构体或 JSON 对象。")
        };
        var children = node.Children.ToDictionary(c => c.Name, StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (!children.Remove(field.Key, out var child)) throw node.Error($"多余或重复的簇字段 [{field.Key}]。");
            if (field.Value is null) throw child.Error("簇字段为空。");
            if (child.Wire.IsCluster) BindCluster(child, field.Value);
            else child.Value = ConvertValue(child, field.Value);
        }
        if (children.Count != 0) throw node.Error($"缺少簇字段 [{string.Join(", ", children.Keys)}]。");
    }

    private static IEnumerable<KeyValuePair<string, object?>> ReadDictionaryFields(Node node, IDictionary dictionary)
    {
        foreach (DictionaryEntry entry in dictionary)
            yield return new(entry.Key as string ?? throw node.Error("簇字段名必须为字符串。"), entry.Value);
    }

    private static IEnumerable<KeyValuePair<string, object?>> ReadClusterFields(Node node, string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw node.Error("整簇输入须为 JSON 对象。");
        foreach (var item in doc.RootElement.EnumerateObject())
            yield return new(item.Name, item.Value);
    }

    internal static object ConvertValue(Node node, object input, bool literal = false)
    {
        try
        {
            if (!node.Wire.IsArray)
            {
                if (literal && node.Wire.Scalar == LVTypeDescCode.String && input is string s &&
                    s.Length >= 2 && s[0] == '"' && s[^1] == '"')
                    return JsonSerializer.Deserialize<string>(s)!;
                return Scalar(node.Wire.Scalar, input);
            }
            IEnumerable items;
            if (input is string json)
            {
                if (json.Length > MaxBytes) throw node.Error("数组文本超过长度上限。");
                input = JsonSerializer.Deserialize<JsonElement>(json);
            }
            if (input is JsonElement { ValueKind: JsonValueKind.Array } arr) items = arr.EnumerateArray();
            else if (input is IEnumerable sequence && input is not IDictionary && input is not Array { Rank: > 1 }) items = sequence;
            else throw node.Error("需要一维数组（常量请填写 JSON 数组，例如 [1,2]）。");
            var converted = new List<object>();
            foreach (object? item in items)
            {
                if (item is null || converted.Count >= MaxElements) throw node.Error("数组包含空值或超过元素上限。");
                converted.Add(Scalar(node.Wire.Scalar, item));
            }
            var result = Array.CreateInstance(ClrType(node.Wire.Scalar), converted.Count);
            ((ICollection)converted).CopyTo(result, 0);
            return result;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or JsonException or InvalidCastException or InvalidOperationException)
        {
            throw node.Error("输入值无法按声明的类型和范围转换。", ex);
        }
    }

    private static object Scalar(LVTypeDescCode type, object value)
    {
        if (value is JsonElement e)
        {
            if ((type == LVTypeDescCode.String && e.ValueKind != JsonValueKind.String) ||
                e.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
                throw new InvalidCastException("需要标量。");
            value = e.ValueKind == JsonValueKind.String ? e.GetString()! : e.ToString();
        }
        if (type == LVTypeDescCode.String)
            return value is string s ? s : throw new InvalidCastException("需要字符串。");
        string txt = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        var inv = CultureInfo.InvariantCulture;
        return type switch
        {
            LVTypeDescCode.Boolean => bool.Parse(txt),
            LVTypeDescCode.Int8 => sbyte.Parse(txt, inv),
            LVTypeDescCode.UInt8 => byte.Parse(txt, inv),
            LVTypeDescCode.Int16 => short.Parse(txt, inv),
            LVTypeDescCode.UInt16 => ushort.Parse(txt, inv),
            LVTypeDescCode.Int32 => int.Parse(txt, inv),
            LVTypeDescCode.UInt32 => uint.Parse(txt, inv),
            LVTypeDescCode.Int64 => long.Parse(txt, inv),
            LVTypeDescCode.UInt64 => ulong.Parse(txt, inv),
            LVTypeDescCode.Float32 => float.TryParse(txt, NumberStyles.Float, inv, out var f) && float.IsFinite(f) ? f : throw new FormatException("需要有限单精度数。"),
            LVTypeDescCode.Float64 => double.TryParse(txt, NumberStyles.Float, inv, out var d) && double.IsFinite(d) ? d : throw new FormatException("需要有限双精度数。"),
            _ => throw new InvalidCastException("不支持的标量类型。")
        };
    }

    private static Type ClrType(LVTypeDescCode code) => code switch
    {
        LVTypeDescCode.Boolean => typeof(bool),
        LVTypeDescCode.Int8 => typeof(sbyte),
        LVTypeDescCode.UInt8 => typeof(byte),
        LVTypeDescCode.Int16 => typeof(short),
        LVTypeDescCode.UInt16 => typeof(ushort),
        LVTypeDescCode.Int32 => typeof(int),
        LVTypeDescCode.UInt32 => typeof(uint),
        LVTypeDescCode.Int64 => typeof(long),
        LVTypeDescCode.UInt64 => typeof(ulong),
        LVTypeDescCode.Float32 => typeof(float),
        LVTypeDescCode.Float64 => typeof(double),
        LVTypeDescCode.String => typeof(string),
        _ => throw new InvalidDataException("不支持的数组元素类型。")
    };

    // The bridge returns NI flattened type descriptors (length in bytes, then flags/type).
    // Preserve the original descriptor for writes; it can include native field names and flags.
    internal static void CheckDescriptor(Node node, short[] descriptor)
    {
        int consumed = CheckPart(node, node.Wire, descriptor, false, out _);
        if (consumed != descriptor.Length) throw node.Error("类型描述符包含额外数据。");
    }

    private static int CheckPart(Node node, ViWireType wire, ReadOnlySpan<short> words, bool checkName, out bool named)
    {
        named = false;
        if (words.Length < 2) throw node.Error("类型描述符不完整。");
        int bytes = (ushort)words[0], code = words[1] & 255;
        if ((bytes & 1) != 0 || bytes < 4 || bytes / 2 > words.Length ||
            code != (int)(wire.IsArray ? LVTypeDescCode.Array : wire.Scalar))
            throw node.Error("真实 VI 类型或描述符长度与配置不符。");
        words = words[..(bytes / 2)];
        int used = 2;
        if (wire.IsArray)
        {
            if (words.Length < 7 || words[2] != 1 || words[3] != -1 || words[4] != -1)
                throw node.Error("仅支持可变长度的一维数组。");
            used = 5 + CheckPart(node, new(wire.Scalar), words[5..], false, out _);
        }
        else if (wire.IsCluster)
        {
            if (words.Length < 3 || words[2] != node.Children.Count) throw node.Error("真实簇字段数量与配置不符。");
            used = 3;
            var unnamed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var child in node.Children)
            {
                used += CheckPart(child, child.Wire, words[used..], true, out bool childNamed);
                if (childNamed) continue;
                string shape = WireShape(child);
                if (!unnamed.TryAdd(shape, child.Name))
                    throw child.Error($"真实簇字段 [{unnamed[shape]}] 与 [{child.Name}] 均未返回标签且编码布局相同，无法核对字段顺序。请使用带标签的端子描述或将字段拆成独立端子。");
            }
        }
        else if (wire.Scalar == LVTypeDescCode.String)
        {
            if (words.Length < 4 || words[2] != -1 || words[3] != -1) throw node.Error("字符串描述符或维度无效。");
            used = 4;
        }
        if (used < words.Length)
        {
            int nameBytes = (ushort)words[used] >> 8;
            if ((nameBytes + 2) / 2 != words.Length - used) throw node.Error("真实端子标签长度异常。");
            named = nameBytes > 0;
            if (checkName && named)
            {
                byte[] label = new byte[nameBytes];
                for (int i = 0; i < label.Length; i++)
                {
                    int index = i + 1;
                    label[i] = (byte)((ushort)words[used + index / 2] >> (index % 2 == 0 ? 8 : 0));
                }
                var ansi = CodePagesEncodingProvider.Instance.GetEncoding(0) ?? Encoding.UTF8;
                if (!string.Equals(ansi.GetString(label), node.Name, StringComparison.Ordinal) &&
                    !string.Equals(Encoding.UTF8.GetString(label), node.Name, StringComparison.Ordinal))
                    throw node.Error("真实簇字段名或顺序与配置不符。");
            }
        }
        return words.Length;
    }

    // Descriptor flags and optional labels aren't part of the wire type. Distinct
    // scalar types, arrays and recursively distinct cluster layouts identify unnamed siblings.
    private static string WireShape(Node node) => node.Wire.IsCluster
        ? "(" + string.Join(",", node.Children.Select(WireShape)) + ")"
        : (node.Wire.IsArray ? "A" : "S") + ((int)node.Wire.Scalar).ToString(CultureInfo.InvariantCulture);

    internal static byte[] Encode(Node node)
    {
        using var buffer = new MemoryStream();
        WriteNode(node, buffer);
        return buffer.ToArray();
    }

    private static void WriteNode(Node node, MemoryStream buffer)
    {
        if (node.Wire.IsCluster)
        {
            foreach (var child in node.Children) WriteNode(child, buffer);
            return;
        }
        if (node.Wire.IsArray)
        {
            var arr = (Array)node.Value!;
            WriteScalar(node, arr.Length, buffer);
            foreach (var item in arr) WriteScalar(node, item!, buffer);
        }
        else WriteScalar(node, node.Value!, buffer);
    }

    private static void WriteScalar(Node node, object val, MemoryStream buffer)
    {
        Span<byte> bytes = stackalloc byte[8];
        int size;
        switch (val)
        {
            case bool v: bytes[0] = (byte)(v ? 1 : 0); size = 1; break;
            case sbyte v: bytes[0] = unchecked((byte)v); size = 1; break;
            case byte v: bytes[0] = v; size = 1; break;
            case short v: BinaryPrimitives.WriteInt16BigEndian(bytes, v); size = 2; break;
            case ushort v: BinaryPrimitives.WriteUInt16BigEndian(bytes, v); size = 2; break;
            case int v: BinaryPrimitives.WriteInt32BigEndian(bytes, v); size = 4; break;
            case uint v: BinaryPrimitives.WriteUInt32BigEndian(bytes, v); size = 4; break;
            case long v: BinaryPrimitives.WriteInt64BigEndian(bytes, v); size = 8; break;
            case ulong v: BinaryPrimitives.WriteUInt64BigEndian(bytes, v); size = 8; break;
            case float v: BinaryPrimitives.WriteSingleBigEndian(bytes, v); size = 4; break;
            case double v: BinaryPrimitives.WriteDoubleBigEndian(bytes, v); size = 8; break;
            case string v:
                int count = TextEncoding.GetByteCount(v);
                if (buffer.Length + 4L + count > MaxBytes) throw node.Error("编码后的输入超过长度上限。");
                WriteScalar(node, count, buffer);
                buffer.Write(TextEncoding.GetBytes(v));
                return;
            default: throw node.Error("输入值尚未绑定。");
        }
        if (buffer.Length + size > MaxBytes) throw node.Error("编码后的输入超过长度上限。");
        buffer.Write(bytes[..size]);
    }

    internal static object Decode(Node node, byte[] data)
    {
        if (data.Length > MaxBytes) throw node.Error("输出超过长度上限。");
        ReadOnlySpan<byte> remaining = data;
        object value = ReadNode(node, ref remaining);
        if (!remaining.IsEmpty) throw node.Error("输出数据长度与配置不符。");
        return value;
    }

    private static object ReadNode(Node node, ref ReadOnlySpan<byte> data)
    {
        if (node.Wire.IsCluster)
        {
            var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var child in node.Children) fields.Add(child.Name, ReadNode(child, ref data));
            return fields;
        }
        var code = node.Wire.Scalar;
        if (!node.Wire.IsArray) return ReadScalar(node, code, ref data);
        int count = ReadLength(node, ref data);
        if (count > MaxElements || count > data.Length / Width(code))
            throw node.Error("数组数量超过实际数据容量或元素上限。");
        var arr = Array.CreateInstance(ClrType(code), count);
        for (int i = 0; i < count; i++) arr.SetValue(ReadScalar(node, code, ref data), i);
        return arr;
    }

    private static int Width(LVTypeDescCode code) => code switch
    {
        LVTypeDescCode.Int8 or LVTypeDescCode.UInt8 or LVTypeDescCode.Boolean => 1,
        LVTypeDescCode.Int16 or LVTypeDescCode.UInt16 => 2,
        LVTypeDescCode.Int64 or LVTypeDescCode.UInt64 or LVTypeDescCode.Float64 => 8,
        _ => 4
    };

    private static ReadOnlySpan<byte> Take(Node node, ref ReadOnlySpan<byte> data, int size)
    {
        if (size < 0 || size > data.Length) throw node.Error("输出长度超出实际返回数据。");
        var bytes = data[..size];
        data = data[size..];
        return bytes;
    }

    private static int ReadLength(Node node, ref ReadOnlySpan<byte> data)
    {
        int count = BinaryPrimitives.ReadInt32BigEndian(Take(node, ref data, 4));
        if (count < 0) throw node.Error("输出长度为负数。");
        return count;
    }

    private static object ReadScalar(Node node, LVTypeDescCode code, ref ReadOnlySpan<byte> data)
    {
        if (code == LVTypeDescCode.String)
        {
            var size = ReadLength(node, ref data);
            try { return TextEncoding.GetString(Take(node, ref data, size)); }
            catch (DecoderFallbackException ex) { throw node.Error("字符串包含无效 UTF-8 数据；二进制请使用 U8 数组。", ex); }
        }
        var b = Take(node, ref data, Width(code));
        return code switch
        {
            LVTypeDescCode.Boolean => b[0] != 0,
            LVTypeDescCode.Int8 => unchecked((sbyte)b[0]),
            LVTypeDescCode.UInt8 => b[0],
            LVTypeDescCode.Int16 => BinaryPrimitives.ReadInt16BigEndian(b),
            LVTypeDescCode.UInt16 => BinaryPrimitives.ReadUInt16BigEndian(b),
            LVTypeDescCode.Int32 => BinaryPrimitives.ReadInt32BigEndian(b),
            LVTypeDescCode.UInt32 => BinaryPrimitives.ReadUInt32BigEndian(b),
            LVTypeDescCode.Int64 => BinaryPrimitives.ReadInt64BigEndian(b),
            LVTypeDescCode.UInt64 => BinaryPrimitives.ReadUInt64BigEndian(b),
            LVTypeDescCode.Float32 => BinaryPrimitives.ReadSingleBigEndian(b),
            LVTypeDescCode.Float64 => BinaryPrimitives.ReadDoubleBigEndian(b),
            _ => throw node.Error("不支持的返回类型。")
        };
    }
}
