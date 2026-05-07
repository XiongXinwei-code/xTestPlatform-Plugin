using System;
using System.IO;
using System.Text;

namespace LabVIEWCallPlugin.LVadapter.Interop
{
    /// <summary>
    /// LabVIEW 展开字符串（Flatten To String）格式辅助类。
    /// 用于在 .NET 类型和 LabVIEW 类型描述符（typeString）及展开数据（dataString）之间进行转换。
    /// 配合 lvLib.dll 中的 LVrte_SetParameters / LVrte_GetParameters 函数使用。
    /// </summary>
    /// <remarks>
    /// 类型描述符遵循 <see cref="LVTypeDescCode"/>（注意：Boolean=0x0021，与 TDMS 的 0x21 一致）。
    /// 数据字节序默认大端序（Big-Endian），与 LabVIEW Flatten To String 默认行为一致。
    /// 若 lvLib.dll 编译时选择了本机字节序（Windows 小端），请将 <see cref="UseBigEndian"/> 设为 false。
    ///
    /// 簇类型描述符格式：
    ///   [total_bytes, 0x0050, num_fields, [4,field1_code], [4,field2_code], ...]
    /// 簇数据格式：
    ///   field1_data + field2_data + ...（无长度前缀，直接拼接）
    /// 一维簇数组数据格式：
    ///   4字节计数（大端）+ cluster0_data + cluster1_data + ...
    /// </remarks>
    public static class LvFlattenHelper
    {
        /// <summary>
        /// 是否使用大端序。LabVIEW Flatten To String 默认为 true（大端）。
        /// 若 lvLib.dll 配置为本机字节序，请设为 false。
        /// </summary>
        public static bool UseBigEndian { get; set; } = true;

        // ── 标量 / 数组类型描述符构建 ─────────────────────────────────────────

        /// <summary>
        /// 为标量类型构建类型描述符（2 个 short，共 4 字节）。
        /// 格式：[total_bytes=4, type_code]
        /// </summary>
        public static short[] MakeScalarTypeString(LVTypeDescCode typeCode)
            => [4, (short)typeCode];

        /// <summary>
        /// 为一维数组类型构建类型描述符（5 个 short，共 10 字节）。
        /// 格式：[total_bytes=10, Array=0x0040, num_dims=1, elem_desc_bytes=4, elem_type_code]
        /// </summary>
        public static short[] Make1DArrayTypeString(LVTypeDescCode elementTypeCode)
            => [10, (short)LVTypeDescCode.Array, 1, 4, (short)elementTypeCode];

        /// <summary>
        /// 为二维数组类型构建类型描述符（6 个 short，共 12 字节）。
        /// </summary>
        public static short[] Make2DArrayTypeString(LVTypeDescCode elementTypeCode)
            => [12, (short)LVTypeDescCode.Array, 2, 4, (short)elementTypeCode, 0];

        // ── 簇类型描述符构建 ──────────────────────────────────────────────────

        /// <summary>
        /// 为所有字段均为标量的簇构建类型描述符。
        /// 格式：[total_bytes, 0x0050, num_fields, [4,field1_code], [4,field2_code], ...]
        /// </summary>
        /// <example>
        /// // 簇：I32 + DBL + String
        /// var ts = LvFlattenHelper.MakeClusterTypeString(
        ///     LVTypeDescCode.Int32, LVTypeDescCode.Float64, LVTypeDescCode.String);
        /// </example>
        public static short[] MakeClusterTypeString(params LVTypeDescCode[] fieldTypes)
        {
            // 每个标量字段 TD = [4, type_code] = 2 shorts
            // 簇头 = [total_bytes, 0x0050, num_fields] = 3 shorts
            int totalShorts = 3 + fieldTypes.Length * 2;
            var td = new short[totalShorts];
            td[0] = (short)(totalShorts * 2);          // total bytes
            td[1] = (short)LVTypeDescCode.Cluster;     // 0x0050
            td[2] = (short)fieldTypes.Length;
            for (int i = 0; i < fieldTypes.Length; i++)
            {
                td[3 + i * 2] = 4;                         // field TD 大小（字节）
                td[3 + i * 2 + 1] = (short)fieldTypes[i];      // field 类型代码
            }
            return td;
        }

        /// <summary>
        /// 为含有复杂字段（如数组、嵌套簇）的簇构建类型描述符。
        /// 每个字段的类型描述符由 <paramref name="fieldTypeDescs"/> 提供。
        /// </summary>
        /// <example>
        /// // 簇：DBL[] + String
        /// var ts = LvFlattenHelper.MakeClusterTypeStringFromDescs(
        ///     LvFlattenHelper.Float64Array1DTypeString,
        ///     LvFlattenHelper.StringTypeString);
        /// </example>
        public static short[] MakeClusterTypeStringFromDescs(params short[][] fieldTypeDescs)
        {
            int fieldTotalShorts = 0;
            foreach (var ftd in fieldTypeDescs) fieldTotalShorts += ftd.Length;

            int totalShorts = 3 + fieldTotalShorts;
            var td = new short[totalShorts];
            td[0] = (short)(totalShorts * 2);
            td[1] = (short)LVTypeDescCode.Cluster;
            td[2] = (short)fieldTypeDescs.Length;

            int pos = 3;
            foreach (var ftd in fieldTypeDescs)
            {
                Array.Copy(ftd, 0, td, pos, ftd.Length);
                pos += ftd.Length;
            }
            return td;
        }

        /// <summary>
        /// 为标量字段簇的一维数组构建类型描述符。
        /// 格式：[total_bytes, 0x0040, 1, cluster_TD...]
        /// </summary>
        public static short[] MakeClusterArray1DTypeString(params LVTypeDescCode[] fieldTypes)
            => MakeClusterArray1DTypeString(MakeClusterTypeString(fieldTypes));

        /// <summary>
        /// 为已有簇类型描述符的一维数组构建类型描述符（支持复杂字段簇）。
        /// </summary>
        public static short[] MakeClusterArray1DTypeString(short[] clusterTypeString)
        {
            int totalShorts = 3 + clusterTypeString.Length;
            var td = new short[totalShorts];
            td[0] = (short)(totalShorts * 2);
            td[1] = (short)LVTypeDescCode.Array;
            td[2] = 1;
            Array.Copy(clusterTypeString, 0, td, 3, clusterTypeString.Length);
            return td;
        }

        // ── 常用类型描述符快捷属性 ────────────────────────────────────────────

        public static short[] BooleanTypeString => MakeScalarTypeString(LVTypeDescCode.Boolean);
        public static short[] I32TypeString => MakeScalarTypeString(LVTypeDescCode.Int32);
        public static short[] I64TypeString => MakeScalarTypeString(LVTypeDescCode.Int64);
        public static short[] U32TypeString => MakeScalarTypeString(LVTypeDescCode.UInt32);
        public static short[] Float32TypeString => MakeScalarTypeString(LVTypeDescCode.Float32);
        public static short[] Float64TypeString => MakeScalarTypeString(LVTypeDescCode.Float64);
        public static short[] StringTypeString => MakeScalarTypeString(LVTypeDescCode.String);
        public static short[] I32Array1DTypeString => Make1DArrayTypeString(LVTypeDescCode.Int32);
        public static short[] Float64Array1DTypeString => Make1DArrayTypeString(LVTypeDescCode.Float64);
        public static short[] StringArray1DTypeString => Make1DArrayTypeString(LVTypeDescCode.String);
        public static short[] RefnumTypeString => MakeScalarTypeString(LVTypeDescCode.Refnum);

        // ── 标量数据编码（.NET → LabVIEW flatten bytes） ─────────────────────

        public static byte[] Encode(bool value)
            => [value ? (byte)1 : (byte)0];

        public static byte[] Encode(short value)
            => HostToLV(BitConverter.GetBytes(value));

        public static byte[] Encode(int value)
            => HostToLV(BitConverter.GetBytes(value));

        public static byte[] Encode(uint value)
            => HostToLV(BitConverter.GetBytes(value));

        public static byte[] Encode(long value)
            => HostToLV(BitConverter.GetBytes(value));

        public static byte[] Encode(float value)
            => HostToLV(BitConverter.GetBytes(value));

        public static byte[] Encode(double value)
            => HostToLV(BitConverter.GetBytes(value));

        /// <summary>
        /// 编码 LabVIEW 字符串：4 字节大端序长度 + ANSI 字节流。
        /// </summary>
        public static byte[] Encode(string value, Encoding? encoding = null)
        {
            encoding ??= Encoding.Default;
            byte[] strBytes = encoding.GetBytes(value ?? string.Empty);
            byte[] result = new byte[4 + strBytes.Length];
            Buffer.BlockCopy(HostToLV(BitConverter.GetBytes(strBytes.Length)), 0, result, 0, 4);
            Buffer.BlockCopy(strBytes, 0, result, 4, strBytes.Length);
            return result;
        }

        /// <summary>
        /// 编码 Refnum（IntPtr → 4 字节 U32，适用于 x86 / 32-bit 进程）。
        /// 用于将 GCHandle.ToIntPtr() 的值作为 LabVIEW Refnum 传给 VI 的 I32/U32 控件。
        /// </summary>
        public static byte[] EncodeRefnum(IntPtr value)
            => HostToLV(BitConverter.GetBytes((uint)value.ToInt32()));

        // ── 数组数据编码 ──────────────────────────────────────────────────────

        /// <summary>编码一维整数数组：4 字节计数（大端）+ 元素数据</summary>
        public static byte[] EncodeArray(int[] values)
        {
            using var ms = new MemoryStream(4 + values.Length * 4);
            ms.Write(HostToLV(BitConverter.GetBytes(values.Length)), 0, 4);
            foreach (var v in values)
                ms.Write(HostToLV(BitConverter.GetBytes(v)), 0, 4);
            return ms.ToArray();
        }

        /// <summary>编码一维双精度数组：4 字节计数（大端）+ 元素数据</summary>
        public static byte[] EncodeArray(double[] values)
        {
            using var ms = new MemoryStream(4 + values.Length * 8);
            ms.Write(HostToLV(BitConverter.GetBytes(values.Length)), 0, 4);
            foreach (var v in values)
                ms.Write(HostToLV(BitConverter.GetBytes(v)), 0, 8);
            return ms.ToArray();
        }

        /// <summary>编码一维字符串数组：4 字节计数 + 各字符串（4字节长度+内容）</summary>
        public static byte[] EncodeArray(string[] values, Encoding? encoding = null)
        {
            using var ms = new MemoryStream();
            ms.Write(HostToLV(BitConverter.GetBytes(values.Length)), 0, 4);
            foreach (var s in values)
            {
                byte[] elem = Encode(s, encoding);
                ms.Write(elem, 0, elem.Length);
            }
            return ms.ToArray();
        }

        // ── 簇数据编码 ────────────────────────────────────────────────────────

        /// <summary>
        /// 编码一个簇：将各字段的编码字节依序拼接（无长度前缀）。
        /// </summary>
        /// <example>
        /// // 簇：I32=10, DBL=3.14, String="OK"
        /// byte[] data = LvFlattenHelper.EncodeCluster(
        ///     LvFlattenHelper.Encode(10),
        ///     LvFlattenHelper.Encode(3.14),
        ///     LvFlattenHelper.Encode("OK"));
        /// </example>
        public static byte[] EncodeCluster(params byte[][] fieldDataList)
        {
            using var ms = new MemoryStream();
            foreach (var field in fieldDataList)
                ms.Write(field, 0, field.Length);
            return ms.ToArray();
        }

        /// <summary>
        /// 编码一维簇数组：4 字节计数（大端）+ 各簇数据块（每簇字节数相同）。
        /// </summary>
        /// <param name="clusterDataList">每个元素是一个簇的编码字节（由 <see cref="EncodeCluster"/> 生成）</param>
        public static byte[] EncodeClusterArray(byte[][] clusterDataList)
        {
            using var ms = new MemoryStream();
            ms.Write(HostToLV(BitConverter.GetBytes(clusterDataList.Length)), 0, 4);
            foreach (var cluster in clusterDataList)
                ms.Write(cluster, 0, cluster.Length);
            return ms.ToArray();
        }

        // ── 标量数据解码（LabVIEW flatten bytes → .NET） ─────────────────────

        public static bool DecodeBoolean(byte[] data, int offset = 0)
            => data[offset] != 0;

        public static sbyte DecodeInt8(byte[] data, int offset = 0)
            => (sbyte)data[offset];

        public static short DecodeInt16(byte[] data, int offset = 0)
        {
            byte[] b = Slice(data, offset, 2);
            if (UseBigEndian && BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToInt16(b, 0);
        }

        public static ushort DecodeUInt16(byte[] data, int offset = 0)
        {
            byte[] b = Slice(data, offset, 2);
            if (UseBigEndian && BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToUInt16(b, 0);
        }

        public static int DecodeInt32(byte[] data, int offset = 0)
        {
            byte[] b = Slice(data, offset, 4);
            if (UseBigEndian && BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToInt32(b, 0);
        }

        public static uint DecodeUInt32(byte[] data, int offset = 0)
        {
            byte[] b = Slice(data, offset, 4);
            if (UseBigEndian && BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToUInt32(b, 0);
        }

        public static long DecodeInt64(byte[] data, int offset = 0)
        {
            byte[] b = Slice(data, offset, 8);
            if (UseBigEndian && BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToInt64(b, 0);
        }

        public static ulong DecodeUInt64(byte[] data, int offset = 0)
        {
            byte[] b = Slice(data, offset, 8);
            if (UseBigEndian && BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToUInt64(b, 0);
        }

        public static float DecodeFloat32(byte[] data, int offset = 0)
        {
            byte[] b = Slice(data, offset, 4);
            if (UseBigEndian && BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToSingle(b, 0);
        }

        public static double DecodeFloat64(byte[] data, int offset = 0)
        {
            byte[] b = Slice(data, offset, 8);
            if (UseBigEndian && BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToDouble(b, 0);
        }

        public static string DecodeString(byte[] data, int offset = 0, Encoding? encoding = null)
        {
            encoding ??= Encoding.Default;
            int length = DecodeInt32(data, offset);
            return length > 0 ? encoding.GetString(data, offset + 4, length) : string.Empty;
        }

        /// <summary>
        /// 解码 Refnum（4 字节 U32 → IntPtr）。
        /// </summary>
        public static IntPtr DecodeRefnum(byte[] data, int offset = 0)
            => new IntPtr((int)DecodeUInt32(data, offset));

        // ── 数组数据解码 ──────────────────────────────────────────────────────

        public static int[] DecodeInt32Array(byte[] data)
        {
            int count = DecodeInt32(data, 0);
            var result = new int[count];
            for (int i = 0; i < count; i++)
                result[i] = DecodeInt32(data, 4 + i * 4);
            return result;
        }

        public static double[] DecodeFloat64Array(byte[] data)
        {
            int count = DecodeInt32(data, 0);
            var result = new double[count];
            for (int i = 0; i < count; i++)
                result[i] = DecodeFloat64(data, 4 + i * 8);
            return result;
        }

        public static string[] DecodeStringArray(byte[] data, Encoding? encoding = null)
        {
            encoding ??= Encoding.Default;
            int count = DecodeInt32(data, 0);
            var result = new string[count];
            int offset = 4;
            for (int i = 0; i < count; i++)
            {
                int len = DecodeInt32(data, offset);
                result[i] = len > 0 ? encoding.GetString(data, offset + 4, len) : string.Empty;
                offset += 4 + len;
            }
            return result;
        }

        // ── 簇数据解码 ────────────────────────────────────────────────────────

        /// <summary>
        /// 解码一个簇，返回各字段值的 .NET 对象数组。
        /// 支持标量字段（bool/int/double 等）及字符串字段；
        /// 嵌套数组和嵌套簇请先用 <see cref="DecodeClusterRaw"/> 获取原始字节再手动解码。
        /// </summary>
        /// <param name="clusterTypeString">簇类型描述符（由 <see cref="MakeClusterTypeString"/> 构建）</param>
        /// <param name="data">展开数据字节流</param>
        /// <param name="offset">数据起始偏移</param>
        /// <param name="encoding">字符串编码，默认 Encoding.Default</param>
        /// <returns>各字段的 .NET 对象（顺序与类型描述符一致）</returns>
        public static object?[] DecodeCluster(
            short[] clusterTypeString, byte[] data, int offset = 0, Encoding? encoding = null)
        {
            var fields = ParseClusterFieldTypes(clusterTypeString);
            var result = new object?[fields.Length];
            int pos = offset;
            for (int i = 0; i < fields.Length; i++)
                result[i] = DecodeFieldAt(fields[i].TypeCode, data, ref pos, encoding);
            return result;
        }

        /// <summary>
        /// 解码一维簇数组，返回每个簇的字段值列表。
        /// </summary>
        /// <param name="clusterTypeString">簇类型描述符</param>
        /// <param name="data">展开数据字节流（含 4 字节计数头）</param>
        /// <param name="encoding">字符串编码</param>
        /// <returns>object?[count][numFields] 二维结果</returns>
        public static object?[][] DecodeClusterArray(
            short[] clusterTypeString, byte[] data, Encoding? encoding = null)
        {
            int count = DecodeInt32(data, 0);
            var result = new object?[count][];
            var fieldTypes = ParseClusterFieldTypes(clusterTypeString);
            int offset = 4;
            for (int i = 0; i < count; i++)
            {
                result[i] = new object?[fieldTypes.Length];
                for (int j = 0; j < fieldTypes.Length; j++)
                    result[i][j] = DecodeFieldAt(fieldTypes[j].TypeCode, data, ref offset, encoding);
            }
            return result;
        }

        /// <summary>
        /// 将簇数据按字段原始字节切分（适用于含嵌套数组/簇的复杂情形）。
        /// 调用方需要自行提供每个字段的字节大小（定长字段），可变长字段（String/Array）不支持。
        /// </summary>
        /// <param name="data">展开数据字节流</param>
        /// <param name="fieldSizes">各字段字节大小（须为定长类型）</param>
        /// <param name="offset">起始偏移</param>
        public static byte[][] DecodeClusterRaw(byte[] data, int[] fieldSizes, int offset = 0)
        {
            var result = new byte[fieldSizes.Length][];
            int pos = offset;
            for (int i = 0; i < fieldSizes.Length; i++)
            {
                result[i] = Slice(data, pos, fieldSizes[i]);
                pos += fieldSizes[i];
            }
            return result;
        }

        // ── 自动编解码 ────────────────────────────────────────────────────────

        /// <summary>
        /// 根据 typeString 中的类型代码自动解码数据，返回对应的 .NET 对象。
        /// 支持标量、字符串、数组（含簇数组）、簇。
        /// </summary>
        public static object? DecodeAuto(short[] typeString, byte[] data, Encoding? encoding = null)
        {
            if (typeString is not { Length: >= 2 }) return null;
            var code = (LVTypeDescCode)(ushort)typeString[1];
            return code switch
            {
                LVTypeDescCode.Boolean => (object)DecodeBoolean(data),
                LVTypeDescCode.Int8 => DecodeInt8(data),
                LVTypeDescCode.Int16 => DecodeInt16(data),
                LVTypeDescCode.Int32 => DecodeInt32(data),
                LVTypeDescCode.Int64 => DecodeInt64(data),
                LVTypeDescCode.UInt8 => data[0],
                LVTypeDescCode.UInt16 => DecodeUInt16(data),
                LVTypeDescCode.UInt32 => DecodeUInt32(data),
                LVTypeDescCode.UInt64 => DecodeUInt64(data),
                LVTypeDescCode.Float32 => DecodeFloat32(data),
                LVTypeDescCode.Float64 => DecodeFloat64(data),
                LVTypeDescCode.String => DecodeString(data, 0, encoding),
                LVTypeDescCode.Refnum => (object)DecodeRefnum(data),
                // 簇：typeString 本身就是簇类型描述符
                LVTypeDescCode.Cluster => (object)DecodeCluster(typeString, data, 0, encoding),
                // 数组：根据元素类型代码（typeString[4]）分派
                LVTypeDescCode.Array when typeString.Length >= 5 =>
                    (LVTypeDescCode)(ushort)typeString[4] switch
                    {
                        LVTypeDescCode.Int32 => (object)DecodeInt32Array(data),
                        LVTypeDescCode.Float64 => DecodeFloat64Array(data),
                        LVTypeDescCode.String => DecodeStringArray(data, encoding),
                        // 簇数组：从数组 TD 中提取嵌套的簇 TD
                        LVTypeDescCode.Cluster => (object)DecodeClusterArray(
                            ExtractElemTDFromArrayTD(typeString), data, encoding),
                        _ => data,
                    },
                _ => data,  // 未知类型返回原始字节
            };
        }

        /// <summary>
        /// 根据值类型自动编码，返回 (typeString, dataString) 元组。
        /// 支持：bool、int、uint、long、float、double、string、int[]、double[]、string[]、<see cref="LvClusterValue"/>。
        /// </summary>
        public static (short[] TypeStr, byte[] DataStr) EncodeAuto(object value, Encoding? encoding = null)
        {
            return value switch
            {
                bool b => (BooleanTypeString, Encode(b)),
                int i => (I32TypeString, Encode(i)),
                uint u => (U32TypeString, Encode(u)),
                long l => (I64TypeString, Encode(l)),
                float f => (Float32TypeString, Encode(f)),
                double d => (Float64TypeString, Encode(d)),
                string s => (StringTypeString, Encode(s, encoding)),
                int[] ia => (I32Array1DTypeString, EncodeArray(ia)),
                double[] da => (Float64Array1DTypeString, EncodeArray(da)),
                string[] sa => (StringArray1DTypeString, EncodeArray(sa, encoding)),
                // 簇：使用 LvClusterValue 携带各字段的 (typeStr, dataStr)
                LvClusterValue cv => BuildClusterEncode(cv),
                IntPtr ptr => (RefnumTypeString, EncodeRefnum(ptr)),
                _ => throw new NotSupportedException($"不支持自动编码的类型: {value.GetType().FullName}"),
            };
        }

        /// <summary>从 typeString 中提取主要类型代码</summary>
        public static LVTypeDescCode GetPrimaryTypeCode(short[] typeString)
            => typeString is { Length: >= 2 }
                ? (LVTypeDescCode)(ushort)typeString[1]
                : LVTypeDescCode.Int32;

        // ── 私有辅助 ──────────────────────────────────────────────────────────

        /// <summary>根据字节序设置将字节数组转换为 LabVIEW 所需字节序</summary>
        private static byte[] HostToLV(byte[] src)
        {
            if (UseBigEndian && BitConverter.IsLittleEndian)
            {
                byte[] copy = (byte[])src.Clone();
                Array.Reverse(copy);
                return copy;
            }
            return src;
        }

        private static byte[] Slice(byte[] src, int offset, int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(src, offset, result, 0, length);
            return result;
        }

        /// <summary>
        /// 从数组类型描述符中提取元素类型描述符。
        /// arrayTD = [total, Array, num_dims, elem_TD...]，elem_TD 从 index=3 开始，
        /// 长度由 arrayTD[3]（elem_TD 总字节数）决定。
        /// </summary>
        private static short[] ExtractElemTDFromArrayTD(short[] arrayTD)
        {
            if (arrayTD.Length < 4) return [];
            int elemTDBytes = arrayTD[3];          // elem TD 的字节数
            int elemTDShorts = elemTDBytes / 2;
            var elemTD = new short[elemTDShorts];
            int copyLen = Math.Min(elemTDShorts, arrayTD.Length - 3);
            Array.Copy(arrayTD, 3, elemTD, 0, copyLen);
            return elemTD;
        }

        /// <summary>
        /// 解析簇类型描述符，返回各字段的 (类型代码, 定长字节数) 列表。
        /// 变长类型（String）的 DataSizeBytes 返回 -1。
        /// </summary>
        private static (LVTypeDescCode TypeCode, int DataSizeBytes)[] ParseClusterFieldTypes(
            short[] clusterTypeString)
        {
            if (clusterTypeString.Length < 3
                || (LVTypeDescCode)(ushort)clusterTypeString[1] != LVTypeDescCode.Cluster)
                throw new ArgumentException("不是有效的簇类型描述符（第 1 位应为 0x0050）",
                    nameof(clusterTypeString));

            int numFields = clusterTypeString[2];
            var result = new (LVTypeDescCode, int)[numFields];
            int pos = 3;  // 第一个字段 TD 的起始位置

            for (int i = 0; i < numFields; i++)
            {
                if (pos + 1 >= clusterTypeString.Length)
                    throw new ArgumentException($"簇类型描述符长度不足，在字段索引 {i}");

                // clusterTypeString[pos]   = 该字段 TD 的总字节数
                // clusterTypeString[pos+1] = 该字段的类型代码
                int fieldTDBytes = clusterTypeString[pos];
                var typeCode = (LVTypeDescCode)(ushort)clusterTypeString[pos + 1];
                result[i] = (typeCode, GetFixedDataSize(typeCode));
                pos += fieldTDBytes / 2;  // 跳过整个字段 TD（字节数 → short 数）
            }
            return result;
        }

        /// <summary>
        /// 从当前偏移处解码一个字段，并将 <paramref name="offset"/> 推进到下一字段起始位置。
        /// </summary>
        private static object? DecodeFieldAt(
            LVTypeDescCode typeCode, byte[] data, ref int offset, Encoding? encoding)
        {
            switch (typeCode)
            {
                case LVTypeDescCode.Boolean:
                    var bv = DecodeBoolean(data, offset); offset += 1; return bv;
                case LVTypeDescCode.Int8:
                    var i8 = DecodeInt8(data, offset); offset += 1; return i8;
                case LVTypeDescCode.UInt8:
                    var u8 = data[offset]; offset += 1; return u8;
                case LVTypeDescCode.Int16:
                    var i16 = DecodeInt16(data, offset); offset += 2; return i16;
                case LVTypeDescCode.UInt16:
                    var u16 = DecodeUInt16(data, offset); offset += 2; return u16;
                case LVTypeDescCode.Int32:
                    var i32 = DecodeInt32(data, offset); offset += 4; return i32;
                case LVTypeDescCode.UInt32:
                    var u32 = DecodeUInt32(data, offset); offset += 4; return u32;
                case LVTypeDescCode.Float32:
                    var f32 = DecodeFloat32(data, offset); offset += 4; return f32;
                case LVTypeDescCode.Int64:
                    var i64 = DecodeInt64(data, offset); offset += 8; return i64;
                case LVTypeDescCode.UInt64:
                    var u64 = DecodeUInt64(data, offset); offset += 8; return u64;
                case LVTypeDescCode.Float64:
                    var f64 = DecodeFloat64(data, offset); offset += 8; return f64;
                case LVTypeDescCode.String:
                    int strLen = DecodeInt32(data, offset);
                    string str = strLen > 0
                        ? (encoding ?? Encoding.Default).GetString(data, offset + 4, strLen)
                        : string.Empty;
                    offset += 4 + strLen;
                    return str;
                default:
                    throw new NotSupportedException(
                        $"簇字段类型 0x{(int)typeCode:X4}（{typeCode}）不被 DecodeCluster 支持，" +
                        $"请改用 DecodeClusterRaw 获取原始字节后手动解码。");
            }
        }

        /// <summary>返回定长类型的字节大小；变长类型（String/Array）返回 -1。</summary>
        private static int GetFixedDataSize(LVTypeDescCode typeCode) => typeCode switch
        {
            LVTypeDescCode.Boolean => 1,
            LVTypeDescCode.Int8 => 1,
            LVTypeDescCode.UInt8 => 1,
            LVTypeDescCode.Int16 => 2,
            LVTypeDescCode.UInt16 => 2,
            LVTypeDescCode.Int32 => 4,
            LVTypeDescCode.UInt32 => 4,
            LVTypeDescCode.Float32 => 4,
            LVTypeDescCode.Refnum => 4,
            LVTypeDescCode.Int64 => 8,
            LVTypeDescCode.UInt64 => 8,
            LVTypeDescCode.Float64 => 8,
            _ => -1,   // String / Array / Cluster 等变长类型
        };

        /// <summary>
        /// 从 <see cref="LvClusterValue"/> 构建 (typeStr, dataStr) 编码元组。
        /// </summary>
        private static (short[], byte[]) BuildClusterEncode(LvClusterValue cv)
        {
            var fieldTDs = new short[cv.Fields.Length][];
            var fieldDatas = new byte[cv.Fields.Length][];
            for (int i = 0; i < cv.Fields.Length; i++)
            {
                fieldTDs[i] = cv.Fields[i].TypeStr;
                fieldDatas[i] = cv.Fields[i].DataStr;
            }
            return (MakeClusterTypeStringFromDescs(fieldTDs), EncodeCluster(fieldDatas));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 代表 LabVIEW 簇的字段集合，用于 <see cref="LvFlattenHelper.EncodeAuto"/> 的自动识别。
    /// 每个字段由 (TypeStr, DataStr) 对描述，可通过 <see cref="From"/> 工厂方法从 .NET 值创建。
    /// </summary>
    /// <example>
    /// // 设置簇参数：{I32=5, DBL=3.14, String="hello"}
    /// var cluster = LvClusterValue.From(5, 3.14, "hello");
    /// var (ts, ds) = LvFlattenHelper.EncodeAuto(cluster);
    /// LvLibWrapper.SafeSetParameter(viRef, "MyCluster", ts, ds, out _, out _);
    /// </example>
    public sealed class LvClusterValue
    {
        /// <summary>各字段的 (typeString, dataString) 对</summary>
        public (short[] TypeStr, byte[] DataStr)[] Fields { get; }

        public LvClusterValue(params (short[] TypeStr, byte[] DataStr)[] fields)
            => Fields = fields;

        /// <summary>
        /// 从 .NET 值列表创建簇（每个字段自动推断类型并编码）。
        /// 支持：bool、int、uint、long、float、double、string、int[]、double[]、string[]。
        /// </summary>
        public static LvClusterValue From(params object[] fieldValues)
        {
            var fields = new (short[], byte[])[fieldValues.Length];
            for (int i = 0; i < fieldValues.Length; i++)
                fields[i] = LvFlattenHelper.EncodeAuto(fieldValues[i]);
            return new LvClusterValue(fields);
        }

        /// <summary>嵌套：簇中包含子簇</summary>
        public static LvClusterValue FromNested(params LvClusterValue[] subClusters)
        {
            var fields = new (short[], byte[])[subClusters.Length];
            for (int i = 0; i < subClusters.Length; i++)
                fields[i] = LvFlattenHelper.EncodeAuto(subClusters[i]);
            return new LvClusterValue(fields);
        }
    }
}