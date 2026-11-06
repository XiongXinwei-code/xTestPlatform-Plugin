using System.Buffers.Binary;
using System.Globalization;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Runtime;

public static class ZlgDataCodec
{
    public static int Size(PdoDataType type) => type switch
    {
        PdoDataType.Bytes => 0, PdoDataType.Boolean or PdoDataType.UInt8 or PdoDataType.Int8 => 1,
        PdoDataType.UInt16 or PdoDataType.Int16 => 2,
        PdoDataType.UInt32 or PdoDataType.Int32 or PdoDataType.Float32 => 4,
        PdoDataType.UInt64 or PdoDataType.Int64 or PdoDataType.Float64 => 8,
        _ => throw new ArgumentException("未知数据类型")
    };
    public static object Decode(byte[] data, PdoDataType type)
    {
        if (type == PdoDataType.Bytes) return data.ToArray();
        if (data.Length != Size(type)) throw new ArgumentException("数据长度与数值类型不匹配");
        return type switch
        {
            PdoDataType.Boolean => data[0] != 0, PdoDataType.UInt8 => data[0], PdoDataType.Int8 => unchecked((sbyte)data[0]),
            PdoDataType.UInt16 => BinaryPrimitives.ReadUInt16LittleEndian(data), PdoDataType.Int16 => BinaryPrimitives.ReadInt16LittleEndian(data),
            PdoDataType.UInt32 => BinaryPrimitives.ReadUInt32LittleEndian(data), PdoDataType.Int32 => BinaryPrimitives.ReadInt32LittleEndian(data),
            PdoDataType.UInt64 => BinaryPrimitives.ReadUInt64LittleEndian(data), PdoDataType.Int64 => BinaryPrimitives.ReadInt64LittleEndian(data),
            PdoDataType.Float32 => BinaryPrimitives.ReadSingleLittleEndian(data), PdoDataType.Float64 => BinaryPrimitives.ReadDoubleLittleEndian(data),
            _ => throw new ArgumentException("未知数据类型")
        };
    }
    public static byte[] Encode(double value, PdoDataType type) => EncodeValue(value, type);

    /// <summary>保留整数精度；整数输出拒绝小数、溢出和隐式字符串/布尔转换。</summary>
    public static byte[] EncodeValue(object value, PdoDataType type)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (type == PdoDataType.Bytes)
            return value is byte[] bytes ? bytes.ToArray() : throw new ArgumentException("Bytes 写入表达式必须返回 byte[]");
        if (type == PdoDataType.Boolean)
            return value is bool boolean ? [(byte)(boolean ? 1 : 0)] : throw new ArgumentException("Boolean 写入表达式必须返回 bool；按位输出请使用数字量输出步骤");
        if (value is not (byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal))
            throw new ArgumentException($"{type} 写入表达式必须返回数值，不能使用字符串、布尔值或数组");
        if (value is double d && !double.IsFinite(d) || value is float f && !float.IsFinite(f))
            throw new ArgumentException("写入值必须是有限数值");
        var data = new byte[Size(type)];
        decimal integral = 0;
        if (type is not (PdoDataType.Float32 or PdoDataType.Float64))
        {
            // Convert.ToDecimal(double/float) rounds significant digits. Check the original
            // floating value first, then convert through an exact integral representation.
            if (value is double floating)
            {
                if (Math.Truncate(floating) != floating) throw new ArgumentException($"{type} 要求整数，不能隐式取整");
                integral = floating < 0 ? checked((long)floating) : checked((ulong)floating);
            }
            else if (value is float singleValue)
            {
                if (MathF.Truncate(singleValue) != singleValue) throw new ArgumentException($"{type} 要求整数，不能隐式取整");
                integral = singleValue < 0 ? checked((long)singleValue) : checked((ulong)singleValue);
            }
            else integral = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            if (decimal.Truncate(integral) != integral)
                throw new ArgumentException($"{type} 要求整数；缩放结果含小数时请在表达式中明确取整，插件不会静默截断");
        }
        checked
        {
            switch (type)
            {
                case PdoDataType.UInt8: data[0] = (byte)integral; break;
                case PdoDataType.Int8:
                    sbyte signedByte = (sbyte)integral;
                    data[0] = unchecked((byte)signedByte); break;
                case PdoDataType.UInt16: BinaryPrimitives.WriteUInt16LittleEndian(data, (ushort)integral); break;
                case PdoDataType.Int16: BinaryPrimitives.WriteInt16LittleEndian(data, (short)integral); break;
                case PdoDataType.UInt32: BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)integral); break;
                case PdoDataType.Int32: BinaryPrimitives.WriteInt32LittleEndian(data, (int)integral); break;
                case PdoDataType.UInt64: BinaryPrimitives.WriteUInt64LittleEndian(data, (ulong)integral); break;
                case PdoDataType.Int64: BinaryPrimitives.WriteInt64LittleEndian(data, (long)integral); break;
                case PdoDataType.Float32:
                    var single = Convert.ToSingle(value, CultureInfo.InvariantCulture); if (!float.IsFinite(single)) throw new OverflowException("Float32 超出范围");
                    BinaryPrimitives.WriteSingleLittleEndian(data, single); break;
                case PdoDataType.Float64: BinaryPrimitives.WriteDoubleLittleEndian(data, Convert.ToDouble(value, CultureInfo.InvariantCulture)); break;
                default: throw new ArgumentException("模拟量仅支持数值类型");
            }
        }
        return data;
    }
    public static ulong Bits(byte[] data, int bitOffset, int bitLength)
    {
        if (bitOffset < 0 || bitLength is < 1 or > 64 || (long)bitOffset + bitLength > (long)data.Length * 8)
            throw new ArgumentException("位读取范围越界");
        ulong result = 0;
        for (int i = 0; i < bitLength; i++) result |= (ulong)((data[(bitOffset + i) / 8] >> ((bitOffset + i) % 8)) & 1) << i;
        return result;
    }
}
