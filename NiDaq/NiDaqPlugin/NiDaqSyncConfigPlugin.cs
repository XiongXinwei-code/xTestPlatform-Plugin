using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqSyncConfigPlugin : StepPluginBase<NiDaqSyncConfigSetting>
{
    public override string StepTypeId => "NiDaq.SyncConfig";
    public override string DisplayName => "NiDaq_Sync_Config";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "配置 NI DAQ 同步采集任务（AI通道+编码器通道、共享时钟/触发），创建任务对象供后续 Start/Read 使用。" +
        "Setting 字段：TaskName(string,表达式), AiChannels(List), EncoderChannels(List), SampleRate(double), SamplesPerChannel(int), SampleMode(enum), ClockSource(string), UseTrigger(bool), TriggerSource(string), TriggerEdge(enum)。";

    public override IStepExecutor CreateExecutor() => new NiDaqSyncConfigExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Sync Config: {s.TaskName} ({s.AiChannels.Count} AI + {s.EncoderChannels.Count} Enc)";
    }
}
