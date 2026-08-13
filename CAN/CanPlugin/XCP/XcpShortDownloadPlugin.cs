using CAN.XCP.Executors;
using CAN.XCP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP;

public sealed class XcpShortDownloadPlugin : StepPluginBase<XcpShortDownloadSetting>
{
    public override string StepTypeId  => "XCP.ShortDownload";
    public override string DisplayName => "XCP_ShortDownload";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        通过 XCP SHORT_DOWNLOAD 命令向 ECU 内存地址写入最多 6 字节数据（标定参数修改）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | "0x7E1" | XCP 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | "0x7E9" | XCP 响应 CAN ID |
        | TimeoutMs | int | 否 | 1000 | 响应超时毫秒数 |
        | Address | string([ExpressionField]) | 是 | "0x40001000" | ECU 内存地址 |
        | AddressExtension | 枚举 | 否 | None | 可选值：None, Odt, Daq |
        | Data | string([ExpressionField]) | 是 | "01 00 00 00" | 要写入的十六进制数据（最多 6 字节） |
        | ByteOrder | 枚举 | 否 | LittleEndian | 可选值：LittleEndian, BigEndian |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 需先通过 XCP_Connect 建立连接；从站返回错误或超时时步骤报错

        ## 相关插件

        - `XCP_Connect`：建立 XCP 连接
        - `XCP_ShortUpload`：从 ECU 内存读取数据
        """;

    public override IStepExecutor CreateExecutor() => new XcpShortDownloadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"XCP ShortDownload Addr={s.Address} Data={s.Data}";
    }
}
