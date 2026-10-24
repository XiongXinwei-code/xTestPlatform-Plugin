using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqTaskStartPlugin : StepPluginBase<NiDaqTaskStartSetting>
{
    public override string StepTypeId => "NiDaq.TaskStart";
    public override string DisplayName => "NiDaq_Task_Start";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_task_start.png";

    public override string Description => """
        ## 功能

        启动已配置的 NI DAQ 任务（AI/编码器/同步采集，或 AO 单点输出）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要启动的任务名称，求值结果为 string |
        | TimeoutMs | int | 否 | 10000 | 名称求值后的 SDK 启动阶段期限（毫秒），包含 I/O 门排队及实际启动；不包含表达式求值 |

        ## 行为

        - 任务不存在时步骤报错
        - AO 任务在此步骤显式启动并写入 AO_Config 时已求值复制的初始电压；可能驱动外部设备动作。启动部分失败仍按 AO 的显式停止策略收尾，不能以 Started=false 假定没有输出。

        ## 相关插件

        - `NiDaq_AI_Config` / `NiDaq_Encoder_Config` / `NiDaq_Sync_Config` / `NiDaq_AO_Config`：配置任务
        - `NiDaq_Task_Stop`：停止任务
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqTaskStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Task Start: {s.TaskName}";
    }
}
