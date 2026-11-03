using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanReadBackgroundStopPlugin : CAN.PluginInfrastructure.StrictStepPluginBase<CanReadBackgroundStopSetting>
{
    public override string StepTypeId => "IO.CanReadBackgroundStop";
    public override string DisplayName => "CAN_Listening_Listen Stop";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_cyclic_stop.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanReadBackgroundStopExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting); return $"停止监听 {s.ConnectionName}/{s.TaskName}，等待发布真实退出";
    }
    public override string Description => """
        ## 功能

        停止指定原始 CAN 或 DBC 总线监听，等待实际发布任务退出；不关闭 CAN 连接、不停止其它任务。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 原监听连接名称 |
        | TaskName | string([ExpressionField]) | 是 | "CanRx1" | 原监听任务名称，DBC 默认 DbcRx1，应与启动配置一致 |
        | TimeoutMs | int | 否 | 5000 | 停止总期限，必须大于 0；超时保留实际任务及输出占用 |
        | IgnoreIfMissing | bool | 否 | false | true 时任务/连接不存在也 PASS，便于独立 Cleanup；false 明确报错 |

        ## 行为

        取消并等待真实退出后才报告 PASS；停止后保留映射变量最后值。不要求其它停止步骤先成功，接收历史故障只记 Warn，不阻止收尾。任务未退出仍报 Error，禁止释放旧发布占用。重复停止已经退出的监听可成功。
        """;
}
