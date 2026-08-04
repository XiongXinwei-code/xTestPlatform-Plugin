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
        "Setting 字段：TaskName(string,表达式,任务名称), Channels(集合,AI通道列表,每个元素结构见下方JSON示例), SampleRate(double,采样率Hz), SamplesPerChannel(int,采样数), " +
        "SampleMode(enum:FiniteSamples|ContinuousSamples), ClockSource(string,时钟源,空为内部时钟), UseTrigger(bool), TriggerSource(string), TriggerEdge(enum:Rising|Falling)。" +
        "Channels 元素JSON示例: {\"PhysicalChannel\":\"Dev1/ai0\",\"ColumnName\":\"CH1\",\"MinValue\":-10.0,\"MaxValue\":10.0,\"Terminal\":\"Differential\"} " +
        "Terminal可选值: Differential, RSE, NRSE, Pseudodifferential。";

    public override IStepExecutor CreateExecutor() => new NiDaqAiConfigExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"AI Config: {s.TaskName} ({s.Channels.Count} ch, {s.SampleRate} Hz)";
    }
}
