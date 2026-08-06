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

        打开 CAN 通道并建立连接，支持 CAN 2.0 Classic、CAN FD、CAN XL 协议。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | AdapterType | 枚举 | 是 | — | 硬件类型，可选值：NI, PEAK, Vector, ZLG |
        | Channel | 表达式(string) | 是 | — | 通道名称 |
        | BaudRate | int | 是 | — | 仲裁段波特率 |
        | Protocol | 枚举 | 是 | Classic | 可选值：Classic, FD, XL |
        | DataBitRate | int | FD/XL 时 | — | 数据段波特率 |
        | ConnectionName | 表达式(string) | 是 | — | 连接标识名，序列内唯一 |

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
        var proto = s.Protocol == CanProtocolType.Classic ? "Classic" : s.Protocol == CanProtocolType.FD ? "FD" : "XL";
        return $"Open {s.ConnectionName} ({s.AdapterType}, {proto}, {s.BaudRate} bps)";
    }
}
