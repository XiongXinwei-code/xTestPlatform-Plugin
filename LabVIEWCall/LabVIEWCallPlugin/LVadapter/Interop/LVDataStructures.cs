using System;
using System.Runtime.InteropServices;

namespace LabVIEWCallPlugin.LVadapter.Interop
{
    // ─────────────────────────────────────────────────────────────────────────
    // LabVIEW 有两套类型编码体系，切勿混用：
    //   LVDataType     → 用于 TDMS / NI 协议层
    //   LVTypeDescCode → 用于 Flatten To String / 类型描述符（本文件核心）
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// LabVIEW TDMS 数据类型代码（NI TDMS 文件格式 / 网络协议层使用）
    /// </summary>
    public enum LVDataType : short
    {
        Void = 0x00,
        Int8 = 0x01,
        Int16 = 0x02,
        Int32 = 0x03,
        Int64 = 0x04,
        UInt8 = 0x05,
        UInt16 = 0x06,
        UInt32 = 0x07,
        UInt64 = 0x08,
        Float32 = 0x09,
        Float64 = 0x0A,
        Extended = 0x0B,
        ComplexFloat32 = 0x0C,
        ComplexFloat64 = 0x0D,
        ComplexExtended = 0x0E,
        Boolean = 0x21,  // TDMS 协议中 Boolean = 0x21
        String = 0x30,
        Path = 0x32,
        Array = 0x40,
        Cluster = 0x50,
        Variant = 0x53,
        Refnum = 0x70
    }

    /// <summary>
    /// LabVIEW 类型描述符编码（用于 Flatten To String / Unflatten From String）
    /// ⚠️ 与 LVDataType(TDMS) 的区别：Boolean 在此处为 0x20，而非 0x21
    /// </summary>
    public enum LVTypeDescCode : ushort
    {
        Int8 = 0x0001,
        Int16 = 0x0002,
        Int32 = 0x0003,
        Int64 = 0x0004,
        UInt8 = 0x0005,
        UInt16 = 0x0006,
        UInt32 = 0x0007,
        UInt64 = 0x0008,
        Float32 = 0x0009,
        Float64 = 0x000A,
        Extended = 0x000B,
        ComplexFloat32 = 0x000C,
        ComplexFloat64 = 0x000D,
        Boolean = 0x0021,  // LabVIEW 类型描述符 Boolean = 0x21（十进制 33）
        String = 0x0030,
        Path = 0x0032,
        Array = 0x0040,
        Cluster = 0x0050,
        Variant = 0x0053,
        Refnum = 0x0070
    }

    /// <summary>
    /// LabVIEW 字符串句柄内容（自定义 DLL 接口结构）
    /// ⚠️ LabVIEW 原生内存布局为 {int32 cnt; char str[];} —— 字符数据内联于 cnt 之后
    ///    此结构仅用于自定义 DLL 接口，不可直接传给 LabVIEW 原生句柄参数
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LVString
    {
        public int Length;
        public IntPtr Data;
    }

    /// <summary>
    /// LabVIEW 数组描述（自定义 DLL 接口结构）
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LVArray
    {
        public int DimensionCount;
        public ushort ElementType;   // 对应 LVTypeDescCode
        public ushort Reserved;
        public IntPtr Dimensions;    // 各维度大小
        public IntPtr Data;
    }

    /// <summary>
    /// LabVIEW 簇描述（自定义 DLL 接口结构）
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LVCluster
    {
        public int ElementCount;
        public IntPtr Elements;
    }

    /// <summary>
    /// LabVIEW Variant 展平数据包装（传递给 DLL 时使用）
    /// 内容 = LabVIEW 类型描述符 + 大端序数据
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LVFlatVariant
    {
        public int DataSize;       // FlattenedData 的字节长度
        public IntPtr FlattenedData; // 指向展平字节流（类型描述符 + 数据）
    }
}