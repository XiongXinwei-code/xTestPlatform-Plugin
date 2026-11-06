using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using ZlgEtherCat.Models;
using ZlgEtherCat.Motion;
using ZlgEtherCat.Native;

namespace ZlgEtherCat.Runtime;

public sealed record EniMappedField(string Name, string EniDataType, int AbsoluteBitOffset, int RelativeBitOffset,
    int BitLength, bool ByteAligned, PdoDataType? ScalarType, string ByteOrder);

/// <summary>仅解释 ENI ProcessImage 元数据；变量名称不作为 CiA402 对象身份依据。</summary>
public sealed class EniProcessImageLayout
{
    private sealed record Field(bool Input, string Name, string Type, int BitOffset, int BitLength, string ByteOrder);
    private readonly Field[] _fields;
    private EniProcessImageLayout(Field[] fields) => _fields = fields;
    public static EniProcessImageLayout Empty { get; } = new([]);
    public bool HasMetadata => _fields.Length > 0;

    public static EniProcessImageLayout Read(Stream stream)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = 64 * 1024 * 1024, CloseInput = false
        });
        var document = XDocument.Load(reader);
        var fields = new List<Field>();
        foreach (var image in document.Descendants().Where(x => x.Name.LocalName == "ProcessImage"))
        foreach (var area in image.Elements().Where(x => x.Name.LocalName is "Inputs" or "Outputs"))
        foreach (var variable in area.Elements().Where(x => x.Name.LocalName == "Variable"))
        {
            string Value(string name) => variable.Elements().FirstOrDefault(x => x.Name.LocalName == name)?.Value.Trim() ?? "";
            int offset = Parse(Value("BitOffs")), length = Parse(Value("BitSize"));
            if (length < 1 || (long)offset + length > 16L * 1024 * 1024 * 8)
                throw new InvalidDataException("ENI 过程映像字段位范围无效或超出插件上限");
            if (fields.Count >= 100000) throw new InvalidDataException("ENI 过程映像字段数量超过插件上限");
            string order = variable.Descendants().FirstOrDefault(x => x.Name.LocalName == "ByteOrder")?.Value.Trim() ?? "";
            fields.Add(new(area.Name.LocalName == "Inputs", Value("Name"), Value("DataType"), offset, length, order));
        }
        return new(fields.ToArray());
    }

    private static int Parse(string text)
    {
        bool hex = text.StartsWith("#x", StringComparison.OrdinalIgnoreCase) || text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (!int.TryParse(hex ? text[2..] : text, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
                CultureInfo.InvariantCulture, out int value) || value < 0)
            throw new InvalidDataException($"ENI 字段位偏移或位宽不是有效非负整数：{text}");
        return value;
    }

    public IReadOnlyList<EniMappedField> Describe(PiRange range, bool input)
    {
        long start = (long)range.Offset * 8, end = start + (long)range.Length * 8;
        return _fields.Where(f => f.Input == input && f.BitOffset >= start && (long)f.BitOffset + f.BitLength <= end)
            .Select(f => new EniMappedField(f.Name, f.Type, f.BitOffset, checked((int)(f.BitOffset - start)), f.BitLength,
                f.BitOffset % 8 == 0 && f.BitLength % 8 == 0, ScalarType(f.Type), f.ByteOrder)).ToArray();
    }

    /// <returns>存在并匹配类型/宽度元数据时为 true；Bytes 原始访问或元数据缺失时为 false。</returns>
    public bool ValidateScalar(int absoluteByteOffset, int length, PdoDataType type, bool input)
    {
        if (type == PdoDataType.Bytes) return false;
        long bitOffset = (long)absoluteByteOffset * 8, end = bitOffset + (long)length * 8;
        var overlaps = _fields.Where(f => f.Input == input && f.BitOffset < end && bitOffset < (long)f.BitOffset + f.BitLength).ToArray();
        if (overlaps.Length == 0) return false;
        if (overlaps.Length != 1 || overlaps[0].BitOffset != bitOffset || overlaps[0].BitLength != (long)length * 8)
            throw new ArgumentException("所选标量范围与 ENI 字段边界/位宽不一致；位变量请使用按位或数字量步骤，原始缓冲使用 Bytes");
        var field = overlaps[0];
        if (field.ByteOrder is not ("" or "0"))
            throw new ArgumentException($"ENI 字段 {field.Name} 的 ByteOrder={field.ByteOrder} 未支持；插件标量仅支持小端");
        if (ScalarType(field.Type) is not { } actual || actual != type)
            throw new ArgumentException($"ENI 字段 {field.Name} 类型为 {field.Type}，与所选 {type} 不一致或尚不支持；不可只按字节数解释");
        if (length != ZlgDataCodec.Size(type)) throw new ArgumentException("映射长度与数据类型宽度不一致");
        return true;
    }

    public void ValidateMotion(MotionAxisConfiguration configuration)
    {
        void Check(bool input, int offset, PdoDataType type, string objectName)
        {
            if (offset < 0) return;
            bool matched = ValidateScalar(offset, ZlgDataCodec.Size(type), type, input);
            if (!matched && _fields.Any(f => f.Input == input))
                throw new ArgumentException($"运动对象 {objectName} 在 ENI {(input ? "输入" : "输出")}字段中没有匹配的字节边界与类型，不能使用此偏移");
        }
        Check(true, configuration.StatusWordOffset, PdoDataType.UInt16, "0x6041:0");
        Check(true, configuration.ActualPositionOffset, PdoDataType.Int32, "0x6064:0");
        Check(true, configuration.ActualVelocityOffset, PdoDataType.Int32, "0x606C:0");
        Check(true, configuration.ActualTorqueOffset, PdoDataType.Int16, "0x6077:0");
        Check(true, configuration.ModeDisplayOffset, PdoDataType.Int8, "0x6061:0");
        Check(false, configuration.ControlWordOffset, PdoDataType.UInt16, "0x6040:0");
        Check(false, configuration.TargetPositionOffset, PdoDataType.Int32, "0x607A:0");
        Check(false, configuration.TargetVelocityOffset, PdoDataType.Int32, "0x60FF:0");
        Check(false, configuration.TargetTorqueOffset, PdoDataType.Int16, "0x6071:0");
        Check(false, configuration.ModeCommandOffset, PdoDataType.Int8, "0x6060:0");
    }

    private static PdoDataType? ScalarType(string type) => type.Trim().ToUpperInvariant() switch
    {
        "BOOL" or "BOOLEAN" => PdoDataType.Boolean,
        "BYTE" or "USINT" or "UINT8" => PdoDataType.UInt8,
        "SINT" or "INT8" => PdoDataType.Int8,
        "WORD" or "UINT" or "UINT16" => PdoDataType.UInt16,
        "INT" or "INT16" => PdoDataType.Int16,
        "DWORD" or "UDINT" or "UINT32" => PdoDataType.UInt32,
        "DINT" or "INT32" => PdoDataType.Int32,
        "LWORD" or "ULINT" or "UINT64" => PdoDataType.UInt64,
        "LINT" or "INT64" => PdoDataType.Int64,
        "REAL" or "REAL32" or "FLOAT32" => PdoDataType.Float32,
        "LREAL" or "REAL64" or "FLOAT64" => PdoDataType.Float64,
        _ => null
    };
}
