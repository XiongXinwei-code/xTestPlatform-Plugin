using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinWritePlugin : StepPluginBase<LinWriteSetting>
{
    public override string StepTypeId   => "IO.LinWrite";
    public override string DisplayName  => "LIN_Write";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin_write.png";

    public override string Description => """
        ## 功能

        向 LIN 总线发送一帧数据（主节点发送帧头和数据）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TimeoutMs | int | 否 | 3000 | 单次驱动调用等待上限，必须大于 0；不包含独立配置的动作后延时。超时返回 Error，未退出原生调用保留隔离并由关闭流程等待 |
        | ConnectionName | string([ExpressionField]) | 是 | "LIN1" | 已打开的连接标识名，求值结果为 string |
        | FrameId | string([ExpressionField]) | 是 | 0 | 帧 ID 0-63，求值结果为字符串，支持十进制（如 "\"63\""）或 0x 前缀十六进制（如 "\"0x3F\""），求值结果为 string |
        | Data | string([ExpressionField]) | 是 | 空 | 十六进制数据，如 "01 02 03"，求值结果为 string |
        | ChecksumType | 枚举 | 否 | Enhanced | 可选值：Classic, Enhanced |

        ## 行为

        - 需先通过 LIN_Open 以主节点模式打开通道

        ## 相关插件

        - `LIN_Open`：打开 LIN 通道
        - `LIN_Read`：接收 LIN 帧
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("LIN", () => new LinWriteExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write ID={s.FrameId} Data={s.Data} ({s.ChecksumType})";
    }
}
