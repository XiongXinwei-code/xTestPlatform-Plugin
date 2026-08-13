using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanOpenPlugin : StepPluginBase<CanOpenSetting>
{
    public override string StepTypeId => "IO.CanOpen";
    public override string DisplayName => "CAN_Open";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        打开 CAN 通道并建立连接，支持 CAN 2.0 Classic、CAN FD 协议。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | AdapterType | 枚举 | 是 | — | 硬件类型，可选值：NI, PEAK, Vector, ZLG, Kvaser, TOSUN |
        | Channel | string([ExpressionField]) | 是 | — | 通道名称 |
        | BaudRate | int | 是 | — | 仲裁段波特率 |
        | Protocol | 枚举 | 是 | Classic | 可选值：Classic, FD |
        | DataBitRate | int | FD 时 | — | 数据段波特率 |
        | ConnectionName | string([ExpressionField]) | 是 | — | 连接标识名，序列内唯一 |

        ## 通道命名规则

        Channel 的格式取决于 AdapterType：

        - NI（NI-XNET）：接口名格式为 `CAN<编号>`，如 `CAN1`、`CAN2`，编号从 1 开始，在 NI MAX 的"设备和接口"中查看（多端口板卡每个端口一个接口名）
        - PEAK（PCAN-Basic）：通道名格式为 `PCAN_USBBUS<编号>`，如 `PCAN_USBBUS1`（也可简写为数字 `1`~`16`），在 PCAN-View 中查看
        - Vector（XL Driver Library）：全局通道索引，如 `0`、`1`，在 Vector Hardware Config 中查看
        - ZLG（周立功）：格式为 `<设备类型>/<设备索引>/<通道索引>`，如 `USBCAN2/0/0`、`USBCANFD-200U/0/1`；设备类型支持 USBCAN1, USBCAN2, USBCAN-E-U, USBCAN-2E-U, USBCANFD-200U, USBCANFD-100U, USBCANFD-MINI
        - Kvaser（CANlib）：通道索引，如 `0`、`1`，在 Kvaser Hardware 工具中查看
        - TOSUN（同星）：通道索引，如 `0`、`1`，默认连接第一个 USB 设备

        注意：运行机器需安装对应厂商驱动（NI-XNET / PCAN-Basic / Vector XL Driver / ZLGCAN / Kvaser Drivers / TSMaster）。

        ## 行为

        - 硬件不存在、通道被占用或同名连接已存在时步骤报错

        ## 相关插件

        - `CAN_Write` / `CAN_Read`：在此连接上收发报文
        - `CAN_Cyclic_SendStart` / `CAN_Cyclic_SendStop`：周期发送
        - `CAN_Close`：关闭本插件打开的通道
        """;

    public override IStepExecutor CreateExecutor() => new CanOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var proto = s.Protocol == CanProtocolType.FD ? "FD" : "Classic";
        return $"Open {s.ConnectionName} ({s.AdapterType}, {proto}, {s.BaudRate} bps)";
    }
}
