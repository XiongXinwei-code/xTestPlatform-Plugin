using CAN.CANopen.Executors;
using CAN.CANopen.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.CANopen;

/// <summary>CANopen 插件共享的分类与图标信息</summary>
internal static class CanopenPluginInfo
{
    public const string Category = "Communication";
    public const string IconPath = "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";
}

/// <summary>CANopen_SdoRead：SDO 读取对象字典条目</summary>
public sealed class CanopenSdoReadPlugin : StepPluginBase<CanopenSdoReadSetting>
{
    public override string StepTypeId => "CANopen.SdoRead";
    public override string DisplayName => "CANopen_SdoRead";
    public override string Category => CanopenPluginInfo.Category;
    public override string IconPath => CanopenPluginInfo.IconPath;

    public override string Description => """
        ## 功能

        通过 SDO 读取 CANopen 节点对象字典中的一个对象（Upload），按数据类型解析后存入变量。支持快速、分段与块传输。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名，求值结果为 string |
        | NodeId | string([ExpressionField]) | 是 | 1 | 节点 ID，1~127，求值结果为 object |
        | Index | string([ExpressionField]) | 是 | 0x1000 | 对象索引，0x0000~0xFFFF，求值结果为 object |
        | SubIndex | string([ExpressionField]) | 是 | 0 | 子索引，0~255，求值结果为 object |
        | DataType | CanopenDataType | 是 | Unsigned32 | Boolean/Integer8~64/Unsigned8~64/Real32/Real64/VisibleString/OctetString |
        | TransferMode | CanopenSdoTransferMode | 否 | Auto | Auto：由节点决定快速或分段传输；Block：块传输 |
        | ResponseTimeoutMs | int | 否 | 1000 | 每帧 SDO 响应超时毫秒数，必须大于 0 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 结果变量，如 Locals.deviceType；写入类型与 DataType 对应（整数/浮点/bool/string，OctetString 为十六进制字符串） |
        | EdsFilePath | string | 否 | 空 | EDS 文件路径，仅用于编辑器中选择对象，运行时不读取 |

        ## 行为

        - 请求 COB-ID 为 0x600+NodeId，响应 COB-ID 为 0x580+NodeId
        - 节点返回 SDO 中止时步骤结果为 Failed，Value 为中止码
        - 超过 ResponseTimeoutMs 未收到响应时步骤报错（Error）
        - 块传输时若节点支持 CRC，会校验 CRC；节点退回普通上传时自动兼容

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `CANopen_SdoWrite`：通过 SDO 写入对象
        """;

    public override IStepExecutor CreateExecutor() => new CanopenSdoReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SDO 读 节点{s.NodeId} {s.Index}sub{s.SubIndex} ({s.DataType}) → {s.ResultVariable}";
    }
}

/// <summary>CANopen_SdoWrite：SDO 写入对象字典条目</summary>
public sealed class CanopenSdoWritePlugin : StepPluginBase<CanopenSdoWriteSetting>
{
    public override string StepTypeId => "CANopen.SdoWrite";
    public override string DisplayName => "CANopen_SdoWrite";
    public override string Category => CanopenPluginInfo.Category;
    public override string IconPath => CanopenPluginInfo.IconPath;

    public override string Description => """
        ## 功能

        通过 SDO 向 CANopen 节点对象字典写入一个对象（Download）。支持快速、分段与块传输。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名，求值结果为 string |
        | NodeId | string([ExpressionField]) | 是 | 1 | 节点 ID，1~127，求值结果为 object |
        | Index | string([ExpressionField]) | 是 | 0x1000 | 对象索引，0x0000~0xFFFF，求值结果为 object |
        | SubIndex | string([ExpressionField]) | 是 | 0 | 子索引，0~255，求值结果为 object |
        | DataType | CanopenDataType | 是 | Unsigned32 | 写入值的编码类型 |
        | Value | string([ExpressionField]) | 是 | 0 | 写入值；整数支持 0x 前缀，OctetString 为十六进制字符串或 byte[]，求值结果为 object |
        | TransferMode | CanopenSdoTransferMode | 否 | Auto | Auto：≤4 字节快速传输，否则分段传输；Block：块传输 |
        | ResponseTimeoutMs | int | 否 | 1000 | 每帧 SDO 响应超时毫秒数，必须大于 0 |
        | EdsFilePath | string | 否 | 空 | EDS 文件路径，仅用于编辑器中选择对象，运行时不读取 |

        ## 行为

        - 写入值超出数据类型范围时步骤报错（Error）
        - 节点返回 SDO 中止时步骤结果为 Failed，Value 为中止码
        - 超过 ResponseTimeoutMs 未收到响应时步骤报错（Error）

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `CANopen_SdoRead`：通过 SDO 读取对象
        """;

    public override IStepExecutor CreateExecutor() => new CanopenSdoWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SDO 写 节点{s.NodeId} {s.Index}sub{s.SubIndex} ({s.DataType}) = {s.Value}";
    }
}

/// <summary>CANopen_NmtControl：发送 NMT 网络管理命令</summary>
public sealed class CanopenNmtControlPlugin : StepPluginBase<CanopenNmtControlSetting>
{
    public override string StepTypeId => "CANopen.NmtControl";
    public override string DisplayName => "CANopen_NmtControl";
    public override string Category => CanopenPluginInfo.Category;
    public override string IconPath => CanopenPluginInfo.IconPath;

    public override string Description => """
        ## 功能

        发送 CANopen NMT 命令，切换节点的网络管理状态。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名，求值结果为 string |
        | NodeId | string([ExpressionField]) | 是 | 1 | 节点 ID，0~127，0 表示所有节点，求值结果为 object |
        | Command | CanopenNmtCommand | 是 | Start | Start/Stop/EnterPreOperational/ResetNode/ResetCommunication |

        ## 行为

        - 以 COB-ID 0x000 发送两字节 NMT 报文，NMT 无应答，发送成功即通过
        - 如需确认节点状态，请在其后使用 `CANopen_WaitHeartbeat`

        ## 相关插件

        - `CANopen_WaitHeartbeat`：等待节点心跳状态
        """;

    public override IStepExecutor CreateExecutor() => new CanopenNmtControlExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"NMT {s.Command} 节点{s.NodeId}";
    }
}

/// <summary>CANopen_WaitHeartbeat：等待节点心跳/状态</summary>
public sealed class CanopenWaitHeartbeatPlugin : StepPluginBase<CanopenWaitHeartbeatSetting>
{
    public override string StepTypeId => "CANopen.WaitHeartbeat";
    public override string DisplayName => "CANopen_WaitHeartbeat";
    public override string Category => CanopenPluginInfo.Category;
    public override string IconPath => CanopenPluginInfo.IconPath;

    public override string Description => """
        ## 功能

        等待 CANopen 节点的心跳（或 Boot-up）报文，并确认节点进入期望的 NMT 状态。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名，求值结果为 string |
        | NodeId | string([ExpressionField]) | 是 | 1 | 节点 ID，1~127，求值结果为 object |
        | ExpectedState | CanopenNmtState | 否 | Operational | Any/BootUp/Stopped/Operational/PreOperational；Any 表示收到任意心跳即通过 |
        | TimeoutMs | int | 否 | 3000 | 等待超时毫秒数，必须大于 0 |
        | StateVariable | string(VariablePathField) | 否 | 空 | 节点状态写入的变量，如 Locals.nmtState，写入类型为 string（状态名） |

        ## 行为

        - 监听 COB-ID 0x700+NodeId，直到收到期望状态
        - 超时仍未收到期望状态时步骤报错（Error），错误信息包含最后收到的状态

        ## 相关插件

        - `CANopen_NmtControl`：发送 NMT 命令
        """;

    public override IStepExecutor CreateExecutor() => new CanopenWaitHeartbeatExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"等待节点{s.NodeId}心跳 {s.ExpectedState}，超时 {s.TimeoutMs} ms";
    }
}

/// <summary>CANopen_PdoSend：发送 PDO</summary>
public sealed class CanopenPdoSendPlugin : StepPluginBase<CanopenPdoSendSetting>
{
    public override string StepTypeId => "CANopen.PdoSend";
    public override string DisplayName => "CANopen_PdoSend";
    public override string Category => CanopenPluginInfo.Category;
    public override string IconPath => CanopenPluginInfo.IconPath;

    public override string Description => """
        ## 功能

        发送一帧 CANopen PDO（通常为节点的 RPDO）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名，求值结果为 string |
        | CobId | string([ExpressionField]) | 是 | 0x201 | PDO 的 COB-ID，0x000~0x7FF，求值结果为 object |
        | Data | string([ExpressionField]) | 是 | "00 00 00 00 00 00 00 00" | 十六进制字符串或 byte[]，0~8 字节，求值结果为 object |

        ## 行为

        - 以标准帧发送，数据按 PDO 映射由用户自行组织（小端）
        - 数据超过 8 字节或 COB-ID 超出范围时步骤报错（Error）

        ## 相关插件

        - `CANopen_PdoReceive`：接收 PDO
        """;

    public override IStepExecutor CreateExecutor() => new CanopenPdoSendExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"PDO 发送 {s.CobId}: {s.Data}";
    }
}

/// <summary>CANopen_PdoReceive：接收 PDO</summary>
public sealed class CanopenPdoReceivePlugin : StepPluginBase<CanopenPdoReceiveSetting>
{
    public override string StepTypeId => "CANopen.PdoReceive";
    public override string DisplayName => "CANopen_PdoReceive";
    public override string Category => CanopenPluginInfo.Category;
    public override string IconPath => CanopenPluginInfo.IconPath;

    public override string Description => """
        ## 功能

        接收一帧指定 COB-ID 的 CANopen PDO（通常为节点的 TPDO），数据以十六进制字符串存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名，求值结果为 string |
        | CobId | string([ExpressionField]) | 是 | 0x181 | PDO 的 COB-ID，0x000~0x7FF，求值结果为 object |
        | TimeoutMs | int | 否 | 1000 | 等待超时毫秒数，必须大于 0 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 结果变量，如 Locals.tpdo1，写入类型为 string（十六进制） |

        ## 行为

        - 超时未收到报文时步骤报错（Error）

        ## 相关插件

        - `CANopen_PdoSend`：发送 PDO
        """;

    public override IStepExecutor CreateExecutor() => new CanopenPdoReceiveExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"PDO 接收 {s.CobId} → {s.ResultVariable}";
    }
}
