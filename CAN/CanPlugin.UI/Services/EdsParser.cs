using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CAN.CANopen;

namespace CAN.UI.Services;

/// <summary>EDS 对象字典中的一个可访问对象</summary>
public sealed class EdsObjectEntry
{
    /// <summary>对象索引</summary>
    public ushort Index { get; init; }
    /// <summary>子索引</summary>
    public byte SubIndex { get; init; }
    /// <summary>参数名（子对象为“父名 / 子名”）</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>数据类型</summary>
    public CanopenDataType DataType { get; init; }
    /// <summary>访问类型（ro/wo/rw/const 等）</summary>
    public string AccessType { get; init; } = string.Empty;

    /// <summary>下拉框显示文本</summary>
    public string Display => $"0x{Index:X4}sub{SubIndex}  {Name}  [{DataType}, {AccessType}]";
}

/// <summary>EDS/DCF 文件解析器（INI 格式，CiA 306）</summary>
public static class EdsParser
{
    /// <summary>对象节名，如 [1018]</summary>
    private static readonly Regex ObjectSection = new("^([0-9A-Fa-f]{4})$", RegexOptions.Compiled);
    /// <summary>子对象节名，如 [1018sub1]</summary>
    private static readonly Regex SubSection = new("^([0-9A-Fa-f]{4})sub([0-9A-Fa-f]{1,2})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>解析 EDS/DCF 文件，返回按 Index/SubIndex 排序的可访问对象列表</summary>
    public static IReadOnlyList<EdsObjectEntry> Parse(string path)
    {
        var sections = ReadIni(path);
        var result = new List<EdsObjectEntry>();

        foreach (var (name, keys) in sections)
        {
            ushort index;
            byte sub;
            string parentName = string.Empty;

            var objMatch = ObjectSection.Match(name);
            var subMatch = SubSection.Match(name);
            if (objMatch.Success)
            {
                // 带子对象的数组/记录本身不可直接读写，只列出其子对象
                if (keys.TryGetValue("SubNumber", out var subNumber) && TryParseInt(subNumber, out var n) && n > 0)
                    continue;
                index = ushort.Parse(objMatch.Groups[1].Value, NumberStyles.HexNumber);
                sub = 0;
            }
            else if (subMatch.Success)
            {
                index = ushort.Parse(subMatch.Groups[1].Value, NumberStyles.HexNumber);
                sub = byte.Parse(subMatch.Groups[2].Value, NumberStyles.HexNumber);
                if (sections.TryGetValue(subMatch.Groups[1].Value.ToUpperInvariant(), out var parent))
                    parent.TryGetValue("ParameterName", out parentName!);
            }
            else
            {
                continue;
            }

            if (!keys.TryGetValue("DataType", out var dt) || !TryParseInt(dt, out var dtCode))
                continue;
            // 不支持的数据类型（如 DOMAIN、Integer24 等）不列入可选对象
            var type = MapDataType(dtCode);
            if (type == null) continue;

            keys.TryGetValue("ParameterName", out var paramName);
            keys.TryGetValue("AccessType", out var access);
            var display = string.IsNullOrEmpty(parentName) ? paramName ?? string.Empty : $"{parentName} / {paramName}";

            result.Add(new EdsObjectEntry
            {
                Index = index,
                SubIndex = sub,
                Name = display,
                DataType = type.Value,
                AccessType = access ?? string.Empty
            });
        }

        return result.OrderBy(e => e.Index).ThenBy(e => e.SubIndex).ToList();
    }

    /// <summary>读取 INI 文件为“节名 → 键值对”字典（忽略大小写和 ; 注释行）</summary>
    private static Dictionary<string, Dictionary<string, string>> ReadIni(string path)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var name = line[1..^1].Trim();
                if (!sections.TryGetValue(name, out current))
                {
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections[name] = current;
                }
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0 || current == null) continue;
            current[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return sections;
    }

    /// <summary>解析十进制或 0x 前缀十六进制整数</summary>
    private static bool TryParseInt(string text, out int value)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>CiA 301 数据类型编码映射为插件数据类型</summary>
    private static CanopenDataType? MapDataType(int code) => code switch
    {
        0x0001 => CanopenDataType.Boolean,
        0x0002 => CanopenDataType.Integer8,
        0x0003 => CanopenDataType.Integer16,
        0x0004 => CanopenDataType.Integer32,
        0x0005 => CanopenDataType.Unsigned8,
        0x0006 => CanopenDataType.Unsigned16,
        0x0007 => CanopenDataType.Unsigned32,
        0x0008 => CanopenDataType.Real32,
        0x0009 => CanopenDataType.VisibleString,
        0x000A => CanopenDataType.OctetString,
        0x0011 => CanopenDataType.Real64,
        0x0015 => CanopenDataType.Integer64,
        0x001B => CanopenDataType.Unsigned64,
        _ => null
    };
}
