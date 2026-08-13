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
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        停止并释放已启动的 NI DAQ 采集任务（通用，适用于 AI/编码器/同步任务）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要停止的任务名称 |

        ## 行为

        - 停止后任务对象被释放，需重新配置才能再次使用

        ## 相关插件

        - `NiDaq_Task_Start`：启动任务
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqTaskStopExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Task Stop: {s.TaskName}";
    }
}
