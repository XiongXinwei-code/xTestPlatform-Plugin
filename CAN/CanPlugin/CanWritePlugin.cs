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
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_write.png";

    public override string Description => """
        ## 功能

        向已打开的 CAN 通道发送一帧报文。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TimeoutMs | int | 否 | 5000 | 发送等待期限，必须大于0；取消/超时后未退出原生调用保留隔离和句柄。成功只表示驱动提交，不代表已上总线 |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名，求值结果为 string |
        | CanId | string([ExpressionField]) | 是 | — | CAN ID，如 0x7DF，求值结果为 string |
        | FrameType | 枚举 | 否 | Standard | 可选值：Standard, Extended |
        | Data | string([ExpressionField]) | 是 | — | 十六进制数据，如 "02 10 01"，求值结果为 string |
        | UseFdFrame | bool | 否 | false | 是否使用 CAN FD 帧 |

        ## 行为

        - 连接不存在或发送失败时步骤报错

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `CAN_Read`：接收报文
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanWriteExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write {s.ConnectionName} ID={s.CanId} [{s.Data}]";
    }
}
