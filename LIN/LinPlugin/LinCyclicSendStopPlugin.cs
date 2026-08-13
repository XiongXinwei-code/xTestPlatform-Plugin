using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinCyclicSendStopPlugin : StepPluginBase<LinCyclicSendStopSetting>
{
    public override string StepTypeId   => "IO.LinCyclicSendStop";
    public override string DisplayName  => "LIN_Cyclic_SendStop";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description => """
        ## 功能

        停止指定名称的 LIN 周期发送任务。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | "LinCyclicTask1" | 要停止的任务标识名 |

        ## 行为

        - 任务不存在时步骤报错

        ## 相关插件

        - `LIN_Cyclic_SendStart`：启动周期发送任务
        """;

    public override IStepExecutor CreateExecutor() => new LinCyclicSendStopExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"CyclicSendStop TaskName={s.TaskName}";
    }
}
