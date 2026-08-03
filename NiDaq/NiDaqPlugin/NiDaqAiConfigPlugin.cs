using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqAiConfigPlugin : StepPluginBase<NiDaqAiConfigSetting>
{
    public override string StepTypeId => "NiDaq.AiConfig";
    public override string DisplayName => "NiDaq_AI_Config";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "配置 NI DAQ AI 模拟输入采集任务（通道、终端、电压范围、时钟、触发），创建任务对象供后续 Start/Read 使用。" +
        "Setting 字段：TaskName(string,表达式,任务名称), Channels(List,AI通道列表), SampleRate(double,采样率), SamplesPerChannel(int,采样数), SampleMode(enum,采样模式), ClockSource(string,时钟源), UseTrigger(bool), TriggerSource(string), TriggerEdge(enum)。";

    public override IStepExecutor CreateExecutor() => new NiDaqAiConfigExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"AI Config: {s.TaskName} ({s.Channels.Count} ch, {s.SampleRate} Hz)";
    }
}
