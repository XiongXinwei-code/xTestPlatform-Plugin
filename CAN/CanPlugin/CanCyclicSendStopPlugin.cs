using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanCyclicSendStopPlugin : StepPluginBase<CanCyclicSendStopSetting>
{
    public override string StepTypeId => "IO.CanCyclicSendStop";
    public override string DisplayName => "CAN_Cyclic_SendStop";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        停止指定的 CAN 周期发送任务。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | CAN 连接标识名 |
        | TaskName | string([ExpressionField]) | 是 | — | 要停止的任务标识名 |

        ## 行为

        - 任务不存在时步骤报错

        ## 相关插件

        - `CAN_Cyclic_SendStart`：启动周期发送任务
        """;

    public override IStepExecutor CreateExecutor() => new CanCyclicSendStopExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"CyclicSendStop {s.ConnectionName} Task={s.TaskName}";
    }
}
