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
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_cyclic_stop.png";

    public override string Description => """
        ## 功能

        停止指定的 CAN 周期发送任务。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | CAN 连接标识名，求值结果为 string |
        | TaskName | string([ExpressionField]) | 是 | — | 要停止的任务标识名，求值结果为 string |

        | TimeoutMs | int | 否 | 5000 | 等待周期任务实际退出的总期限，必须大于 0；超时保留任务与连接隔离，不报告已停止 |

        ## 行为

        - 按 ConnectionName + TaskName 定位任务，取消并等待实际工作退出，默认期限 5000 ms
        - 连接/任务不存在或后台已故障时报错；超时保留任务与隔离，不能覆盖重启
        - 本步骤确认主机工作退出，不代表硬件队列已排空或电机已停止

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
