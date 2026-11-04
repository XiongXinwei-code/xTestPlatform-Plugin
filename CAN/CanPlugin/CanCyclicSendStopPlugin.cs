using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanCyclicSendStopPlugin : CAN.PluginInfrastructure.StrictStepPluginBase<CanCyclicSendStopSetting>
{
    public override string StepTypeId => "IO.CanCyclicSendStop";
    public override string DisplayName => "CAN_Cyclic_SendStop";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_cyclic_stop.png";

    public override string Description => """
        ## 功能

        `CAN > Cyclic > SendStop`（`IO.CanCyclicSendStop`）是原始 CAN 周期发送和 DBC 周期发送共用的停止步骤。要停止 `DBC Cyclic Write`（`IO.CanDbcCyclicWrite`）启动的任务，在序列中添加本步骤。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | CAN 连接标识名，求值结果必须与启动步骤相同 |
        | TaskName | string([ExpressionField]) | 是 | "CyclicTask1" | 要停止的任务标识名，求值结果必须与启动步骤相同；DBC 默认任务名为 "DbcTask1"，需按启动配置修改 |
        | TimeoutMs | int | 否 | 5000 | 本步骤停止总期限，必须大于 0；超时保留任务，真实写调用未退出时保持连接隔离，不报告已停止 |

        ## 行为

        - 按 ConnectionName + TaskName 定位任务，取消并等待实际工作退出，默认期限 5000 ms
        - 示例：DBC 启动时使用 `ConnectionName = "CAN1"`、`TaskName = "DbcTask1"`，则在本步骤也填写这两个表达式。任务改名时使用实际任务名，仅停止当前归属该任务的报文 ID。
        - 连接/任务不存在时报错；后台历史错误在实际写调用退出后记 Warn 并释放任务名。实际写未退出仍不能报告已停止，不影响其它任务的正常收尾
        - 本步骤确认主机工作退出，不代表硬件队列已排空或电机已停止

        ## 相关插件

        - `CAN > Cyclic > SendStart`（`IO.CanCyclicSendStart`）：原始 CAN 周期发送的启动步骤。
        - `CAN > DBC > DBC Cyclic Write`（`IO.CanDbcCyclicWrite`）：DBC 周期发送的启动步骤。`DBC Listen Stop` 用于停止后台监听。
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanCyclicSendStopExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"CyclicSendStop {s.ConnectionName} Task={s.TaskName}";
    }
}
