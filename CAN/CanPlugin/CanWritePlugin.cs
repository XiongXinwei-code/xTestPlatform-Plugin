using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanWritePlugin : StepPluginBase<CanWriteSetting>
{
    public override string StepTypeId => "IO.CanWrite";
    public override string DisplayName => "CAN_Write";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        向已打开的 CAN 通道发送一帧报文。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | 表达式(string) | 是 | — | 已打开的 CAN 连接名 |
        | CanId | 表达式(string) | 是 | — | CAN ID，如 0x7DF |
        | FrameType | 枚举 | 否 | Standard | 可选值：Standard, Extended |
        | Data | 表达式(string) | 是 | — | 十六进制数据，如 "02 10 01" |
        | UseFdFrame | bool | 否 | false | 是否使用 CAN FD 帧 |
        | EnableLog | bool | 否 | true | 是否输出发送日志 |

        ## 行为

        - 连接不存在或发送失败时步骤报错

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `CAN_Read`：接收报文
        """;

    public override IStepExecutor CreateExecutor() => new CanWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write {s.ConnectionName} ID={s.CanId} [{s.Data}]";
    }
}
