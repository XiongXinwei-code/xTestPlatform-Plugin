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
        "Setting 字段：TaskName(string,表达式), AiChannels(集合,AI通道列表), EncoderChannels(集合,编码器通道列表), SampleRate(double), SamplesPerChannel(int), " +
        "SampleMode(enum:FiniteSamples|ContinuousSamples), ClockSource(string), UseTrigger(bool), TriggerSource(string), TriggerEdge(enum:Rising|Falling)。" +
        "AiChannels 元素JSON示例: {\"PhysicalChannel\":\"Dev1/ai0\",\"ColumnName\":\"CH1\",\"MinValue\":-10.0,\"MaxValue\":10.0,\"Terminal\":\"Differential\"} " +
        "Terminal可选值: Differential, RSE, NRSE, Pseudodifferential。" +
        "EncoderChannels 元素JSON示例: {\"CounterChannel\":\"Dev1/ctr0\",\"ColumnName\":\"ENC1\",\"DecodingType\":\"X4\",\"PulsesPerRevolution\":1024,\"DistancePerPulse\":0.3515625,\"Unit\":\"Degrees\",\"ZIndexEnable\":false} " +
        "DecodingType可选值: X1, X2, X4。Unit可选值: Pulses, Degrees, Millimeters。";

    public override IStepExecutor CreateExecutor() => new NiDaqSyncConfigExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Sync Config: {s.TaskName} ({s.AiChannels.Count} AI + {s.EncoderChannels.Count} Enc)";
    }
}
