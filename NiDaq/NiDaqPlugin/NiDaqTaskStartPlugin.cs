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

    public override string Description =>
        "启动已配置的 NI DAQ 采集任务（通用，适用于 AI/编码器/同步任务）。" +
        "Setting 字段：TaskName(string,表达式,要启动的任务名称)。";

    public override IStepExecutor CreateExecutor() => new NiDaqTaskStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Task Start: {s.TaskName}";
    }
}
