using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqTaskStopPlugin : StepPluginBase<NiDaqTaskStopSetting>
{
    public override string StepTypeId => "NiDaq.TaskStop";
    public override string DisplayName => "NiDaq_Task_Stop";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_task_stop.png";

    public override string Description => """
        ## 功能

        停止并释放 NI DAQ 任务（AI/编码器/同步采集或 AO 单点输出，亦可清理配置/启动失败的任务）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要停止的任务名称，求值结果为 string |
        | TimeoutMs | int | 否 | 5000 | 名称求值后的停止等待期限（毫秒），包含原始 I/O 退出及实际收尾；不包含表达式求值，等待结束不代表已撤销原调用 |

        ## 行为

        - 任务不存在时返回 Error，请确认 TaskName 与配置步骤创建的名称一致。


        - 先冻结当前代次的新操作，再等待真实任务和 SDK 释放；只在确认释放后移除自己的资源。超时/取消不代表底层已退出，未完成或关闭失败的 owner 保留诊断与重试入口；立即重跑将按打开/配置步骤期限等待旧收尾，不强制关闭其他活动连接。

        - 停止后任务对象被释放，需重新配置才能再次使用；非 AO 的 Abort 返回错误不阻止实际 I/O 退出后的合法 Dispose，历史诊断记 Warn。实际 Dispose 失败保留原实例，不以第二次空操作假报成功
        - AO 不并发 Abort 正在执行的 writer：等待真实调用后，按 AO_Config 的 StopPolicy 处理指定终值或设备默认行为，再 Stop/释放。其 CleanupTimeoutMs 限制真实输出收尾；本步骤 TimeoutMs 只限制本次等待。
        - 指定终值失败/结果未知时，即使 Stop 已返回也保留隔离与错误，不盲重发或假报电气安全。正常重试仅允许已确认终值的后续 Stop/释放，或此前期限到而尚未提交的清理；真正未退出的调用继续保留引用。

        ## 相关插件

        - `NiDaq_Task_Start`：启动任务
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("NiDaq", () => new NiDaqTaskStopExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Task Stop: {s.TaskName}";
    }
}
