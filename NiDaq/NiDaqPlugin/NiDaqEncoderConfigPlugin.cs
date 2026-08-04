using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqEncoderConfigPlugin : StepPluginBase<NiDaqEncoderConfigSetting>
{
    public override string StepTypeId => "NiDaq.EncoderConfig";
    public override string DisplayName => "NiDaq_Encoder_Config";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "配置 NI DAQ 编码器采集任务（Counter通道、解码类型、脉冲数、单位），创建任务对象供后续 Start/Read 使用。" +
        "Setting 字段：TaskName(string,表达式,任务名称), CounterChannel(string,表达式,Counter通道如Dev1/ctr0), " +
        "DecodingType(枚举:X1/X2/X4,默认X4), PulsesPerRevolution(int,每转脉冲数PPR,默认1024), " +
        "ZIndexEnable(bool,是否启用Z索引复位,默认false), DistancePerPulse(double,每脉冲对应的距离/角度,默认0.3515625), " +
        "Unit(枚举:Pulses/Degrees/Millimeters,默认Degrees)。";

    public override IStepExecutor CreateExecutor() => new NiDaqEncoderConfigExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Encoder Config: {s.TaskName} ({s.CounterChannel}, {s.DecodingType})";
    }
}
