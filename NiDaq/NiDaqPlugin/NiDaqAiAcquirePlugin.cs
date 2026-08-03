using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqAiAcquirePlugin : StepPluginBase<NiDaqAiAcquireSetting>
{
    public override string StepTypeId => "NiDaq.AiAcquire";
    public override string DisplayName => "NiDaq_AI_Acquire";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "执行 NI DAQ 模拟输入有限采集（单次采集 N 个样本）。" +
        "Setting 字段：Channels(列表,每项含PhysicalChannel/ColumnName/MinValue/MaxValue/Terminal), " +
        "SampleRate(double,采样率Hz), SamplesPerChannel(int,每通道采样数), ResultVariablePrefix(string,表达式,结果变量前缀)。";

    public override IStepExecutor CreateExecutor() => new NiDaqAiAcquireExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"AI Acquire: {s.Channels.Count} ch @ {s.SampleRate}Hz × {s.SamplesPerChannel}";
    }
}
