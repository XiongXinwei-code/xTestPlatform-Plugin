using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinWakeupPlugin : StepPluginBase<LinWakeupSetting>
{
    public override string StepTypeId   => "IO.LinWakeup";
    public override string DisplayName  => "LIN_Wakeup";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description => """
        ## 功能

        唤醒 LIN 总线。远程唤醒模式在总线上发送唤醒模式（Wakeup Pattern）以唤醒所有处于睡眠状态的节点；本地唤醒模式仅唤醒本地接口，不影响总线上的其他节点。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "LIN1" | 要唤醒的连接标识名 |
        | WakeupMode | LinWakeupMode | 是 | Remote | 唤醒模式：Remote（总线唤醒）/ Local（仅本地接口） |

        ## 行为

        - 必须在 `LIN_Open` 之后使用
        - Remote 模式向总线发送唤醒模式，随后本地接口自动进入唤醒状态
        - Local 模式仅将本地接口置为唤醒状态，不发送任何总线信号

        ## 示例

        总线进入睡眠后，执行本步骤（Remote 模式）唤醒所有从节点，再继续收发帧。

        ## 相关插件

        - `LIN_Open`：打开 LIN 通道
        - `LIN_Write`：发送 LIN 帧
        """;

    public override IStepExecutor CreateExecutor() => new LinWakeupExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Wakeup {s.ConnectionName} ({(s.WakeupMode == LinWakeupMode.Remote ? "总线唤醒" : "本地唤醒")})";
    }
}
