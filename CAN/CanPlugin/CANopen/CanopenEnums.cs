using System.Text.Json.Serialization;

namespace CAN.CANopen;

/// <summary>CANopen 对象字典数据类型（CiA 301）</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanopenDataType
{
    /// <summary>布尔（1 字节）</summary>
    Boolean = 0,
    /// <summary>有符号 8 位整数</summary>
    Integer8 = 1,
    /// <summary>有符号 16 位整数</summary>
    Integer16 = 2,
    /// <summary>有符号 32 位整数</summary>
    Integer32 = 3,
    /// <summary>有符号 64 位整数</summary>
    Integer64 = 4,
    /// <summary>无符号 8 位整数</summary>
    Unsigned8 = 5,
    /// <summary>无符号 16 位整数</summary>
    Unsigned16 = 6,
    /// <summary>无符号 32 位整数</summary>
    Unsigned32 = 7,
    /// <summary>无符号 64 位整数</summary>
    Unsigned64 = 8,
    /// <summary>32 位单精度浮点</summary>
    Real32 = 9,
    /// <summary>64 位双精度浮点</summary>
    Real64 = 10,
    /// <summary>可见字符串（ASCII）</summary>
    VisibleString = 11,
    /// <summary>字节串（以十六进制字符串表示）</summary>
    OctetString = 12
}

/// <summary>SDO 传输方式</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanopenSdoTransferMode
{
    /// <summary>自动：≤4 字节使用快速传输，否则使用分段传输</summary>
    Auto = 0,
    /// <summary>块传输</summary>
    Block = 1
}

/// <summary>NMT 命令</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanopenNmtCommand
{
    /// <summary>启动节点（进入 Operational）</summary>
    Start = 0,
    /// <summary>停止节点（进入 Stopped）</summary>
    Stop = 1,
    /// <summary>进入预运行（Pre-Operational）</summary>
    EnterPreOperational = 2,
    /// <summary>复位节点</summary>
    ResetNode = 3,
    /// <summary>复位通信</summary>
    ResetCommunication = 4
}

/// <summary>NMT 节点状态</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanopenNmtState
{
    /// <summary>任意状态（收到心跳即可）</summary>
    Any = 0,
    /// <summary>启动报文（状态字节 0x00）</summary>
    BootUp = 1,
    /// <summary>停止（状态字节 0x04）</summary>
    Stopped = 2,
    /// <summary>运行（状态字节 0x05）</summary>
    Operational = 3,
    /// <summary>预运行（状态字节 0x7F）</summary>
    PreOperational = 4
}
