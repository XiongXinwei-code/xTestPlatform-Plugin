using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.CANopen.Models;

/// <summary>CANopen 步骤公共设置</summary>
[MessagePackObject(true)]
public class CanopenCommonSetting
{
    /// <summary>CAN 连接标识名（引用 CAN_Open 创建的连接）</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"CAN1\"";
}

/// <summary>SDO 读写步骤公共设置</summary>
[MessagePackObject(true)]
public class CanopenSdoSettingBase : CanopenCommonSetting
{
    /// <summary>目标节点 ID（1~127）</summary>
    [ExpressionField]
    public string NodeId { get; set; } = "1";

    /// <summary>对象字典索引</summary>
    [ExpressionField]
    public string Index { get; set; } = "0x1000";

    /// <summary>对象字典子索引</summary>
    [ExpressionField]
    public string SubIndex { get; set; } = "0";

    /// <summary>数据类型</summary>
    public CanopenDataType DataType { get; set; } = CanopenDataType.Unsigned32;

    /// <summary>传输方式</summary>
    public CanopenSdoTransferMode TransferMode { get; set; } = CanopenSdoTransferMode.Auto;

    /// <summary>每帧 SDO 响应超时 (ms)</summary>
    public int ResponseTimeoutMs { get; set; } = 1000;

    /// <summary>EDS 文件路径（仅供编辑器选择对象使用，运行时不读取）</summary>
    public string EdsFilePath { get; set; } = string.Empty;
}

/// <summary>CANopen_SdoRead 步骤设置</summary>
[MessagePackObject(true)]
public class CanopenSdoReadSetting : CanopenSdoSettingBase
{
    /// <summary>读取结果写入的变量</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = string.Empty;
}

/// <summary>CANopen_SdoWrite 步骤设置</summary>
[MessagePackObject(true)]
public class CanopenSdoWriteSetting : CanopenSdoSettingBase
{
    /// <summary>写入值</summary>
    [ExpressionField]
    public string Value { get; set; } = "0";
}

/// <summary>CANopen_NmtControl 步骤设置</summary>
[MessagePackObject(true)]
public class CanopenNmtControlSetting : CanopenCommonSetting
{
    /// <summary>目标节点 ID（0 表示广播到所有节点）</summary>
    [ExpressionField]
    public string NodeId { get; set; } = "1";

    /// <summary>NMT 命令</summary>
    public CanopenNmtCommand Command { get; set; } = CanopenNmtCommand.Start;
}

/// <summary>CANopen_WaitHeartbeat 步骤设置</summary>
[MessagePackObject(true)]
public class CanopenWaitHeartbeatSetting : CanopenCommonSetting
{
    /// <summary>目标节点 ID（1~127）</summary>
    [ExpressionField]
    public string NodeId { get; set; } = "1";

    /// <summary>期望状态</summary>
    public CanopenNmtState ExpectedState { get; set; } = CanopenNmtState.Operational;

    /// <summary>等待超时 (ms)</summary>
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>收到的节点状态写入的变量</summary>
    [VariablePathField]
    public string StateVariable { get; set; } = string.Empty;
}

/// <summary>CANopen_PdoSend 步骤设置</summary>
[MessagePackObject(true)]
public class CanopenPdoSendSetting : CanopenCommonSetting
{
    /// <summary>PDO 的 COB-ID</summary>
    [ExpressionField]
    public string CobId { get; set; } = "0x201";

    /// <summary>PDO 数据（十六进制字符串或 byte[]）</summary>
    [ExpressionField]
    public string Data { get; set; } = "\"00 00 00 00 00 00 00 00\"";
}

/// <summary>CANopen_SyncSend 步骤设置</summary>
[MessagePackObject(true)]
public class CanopenSyncSendSetting : CanopenCommonSetting
{
    /// <summary>SYNC 的 COB-ID（默认 0x80）</summary>
    [ExpressionField]
    public string CobId { get; set; } = "0x80";

    /// <summary>SYNC 计数器（0 表示不带计数器，1~240 时发送 1 字节计数器）</summary>
    [ExpressionField]
    public string Counter { get; set; } = "0";
}

/// <summary>CANopen_EmcyReceive 步骤设置</summary>
[MessagePackObject(true)]
public class CanopenEmcyReceiveSetting : CanopenCommonSetting
{
    /// <summary>目标节点 ID（1~127）</summary>
    [ExpressionField]
    public string NodeId { get; set; } = "1";

    /// <summary>期望的错误码（留空表示收到任意 EMCY 即通过）</summary>
    [ExpressionField]
    public string ExpectedErrorCode { get; set; } = string.Empty;

    /// <summary>等待超时 (ms)</summary>
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>错误码写入的变量（十六进制字符串，如 0x3210）</summary>
    [VariablePathField]
    public string ErrorCodeVariable { get; set; } = string.Empty;

    /// <summary>错误寄存器写入的变量（十六进制字符串，如 0x04）</summary>
    [VariablePathField]
    public string ErrorRegisterVariable { get; set; } = string.Empty;

    /// <summary>厂商自定义数据写入的变量（十六进制字符串）</summary>
    [VariablePathField]
    public string ManufacturerDataVariable { get; set; } = string.Empty;
}

/// <summary>CANopen_PdoReceive 步骤设置</summary>
[MessagePackObject(true)]
public class CanopenPdoReceiveSetting : CanopenCommonSetting
{
    /// <summary>PDO 的 COB-ID</summary>
    [ExpressionField]
    public string CobId { get; set; } = "0x181";

    /// <summary>等待超时 (ms)</summary>
    public int TimeoutMs { get; set; } = 1000;

    /// <summary>接收数据写入的变量（十六进制字符串）</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = string.Empty;
}
