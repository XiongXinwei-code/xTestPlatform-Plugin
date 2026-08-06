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
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        启动已配置的 NI DAQ 采集任务（通用，适用于 AI/编码器/同步任务）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | 表达式(string) | 是 | — | 要启动的任务名称 |

        ## 行为

        - 任务不存在时步骤报错

        ## 相关插件

        - `NiDaq_AI_Config` / `NiDaq_Encoder_Config` / `NiDaq_Sync_Config`：配置任务
        - `NiDaq_Task_Stop`：停止任务
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqTaskStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Task Start: {s.TaskName}";
    }
}
