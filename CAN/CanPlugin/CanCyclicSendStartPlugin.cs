using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanCyclicSendStartPlugin : StepPluginBase<CanCyclicSendStartSetting>
{
    public override string StepTypeId => "IO.CanCyclicSendStart";
    public override string DisplayName => "CAN_Cyclic_SendStart";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        启动 CAN 周期发送任务，按配置的报文列表持续循环发送 CAN 帧，直到执行 CAN_Cyclic_SendStop 停止。用于模拟整车网络环境（如车速、转速等信号）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TaskName | string([ExpressionField]) | 是 | — | 任务标识名，Stop 时用此名称停止 |
        | EnableLog | bool | 否 | false | 是否输出发送日志 |
        | Messages | 集合 | 是 | — | 周期报文列表，元素结构见示例 |

        Messages 元素中 CanId 和 Data 为表达式字段，字面量值需用引号包裹；FrameType 可选值：Standard, Extended。

        ## 行为

        - 步骤启动任务后立即返回，发送在后台持续进行
        - 同名 TaskName 已在运行时步骤报错

        ## 示例

        ```json
        {
          "ConnectionName": "\"CAN1\"",
          "TaskName": "\"cyclic1\"",
          "Messages": [
            { "CanId": "\"0x185\"", "FrameType": "Standard", "Data": "\"FF FF FF FF FF FF FF FF\"", "CycleTimeMs": 100, "Enabled": true }
          ]
        }
        ```

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `CAN_Cyclic_SendStop`：停止本插件启动的周期发送任务
        """;

    public override IStepExecutor CreateExecutor() => new CanCyclicSendStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var enabledCount = s.Messages.Count(m => m.Enabled);
        return $"CyclicSendStart {s.ConnectionName} Task={s.TaskName} ({enabledCount} messages)";
    }
}
