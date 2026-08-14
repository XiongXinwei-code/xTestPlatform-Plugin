using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinSleepPlugin : StepPluginBase<LinSleepSetting>
{
    public override string StepTypeId   => "IO.LinSleep";
    public override string DisplayName  => "LIN_Sleep";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description => """
        ## 功能

        使 LIN 总线进入睡眠状态。远程睡眠模式由主节点发送 Go-to-Sleep 命令（ID 0x3C 诊断帧，首字节 0x00），使总线上所有从节点进入低功耗睡眠；本地睡眠模式仅将本地接口置为睡眠态，不发送任何总线信号。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "LIN1" | 连接标识名 |
        | SleepMode | LinSleepMode | 是 | Remote | 睡眠模式：Remote（发送 Go-to-Sleep 命令）/ Local（仅本地接口） |
        | PostSleepDelayMs | int | 是 | 100 | 入睡后延时（毫秒），等待从节点完成入睡流程；0 表示不延时 |

        ## 行为

        - 必须在 `LIN_Open` 之后使用
        - Remote 模式要求通道以主节点模式打开（Go-to-Sleep 命令只能由主节点发送）
        - 入睡后按 PostSleepDelayMs 延时后才返回，便于紧接着进行睡眠电流测量等操作
        - 需要恢复通信时使用 `LIN_Wakeup` 步骤

        ## 示例

        正常通信结束后执行本步骤（Remote 模式）使被测 ECU 入睡，随后测量其睡眠电流，再用 `LIN_Wakeup` 唤醒验证通信恢复。

        ## 相关插件

        - `LIN_Wakeup`：唤醒 LIN 总线
        - `LIN_Open`：打开 LIN 通道
        """;

    public override IStepExecutor CreateExecutor() => new LinSleepExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Sleep {s.ConnectionName} ({(s.SleepMode == LinSleepMode.Remote ? "总线睡眠" : "本地睡眠")}, 延时 {s.PostSleepDelayMs}ms)";
    }
}
