using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqEncoderReadPlugin : StepPluginBase<NiDaqEncoderSetting>
{
    public override string StepTypeId => "NiDaq.EncoderRead";
    public override string DisplayName => "NiDaq_Encoder_Read";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "读取 NI DAQ 正交编码器的当前位置/角度/位移值。" +
        "Setting 字段：CounterChannel(string,表达式,如Dev1/ctr0), DecodingType(枚举X1/X2/X4), " +
        "PulsesPerRevolution(int,PPR), ZIndexEnable(bool), DistancePerPulse(double), " +
        "Unit(枚举Pulses/Degrees/Millimeters), ResultVariable(string,表达式)。";

    public override IStepExecutor CreateExecutor() => new NiDaqEncoderReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Encoder Read: {s.CounterChannel} ({s.DecodingType}, {s.Unit}) → {s.ResultVariable}";
    }
}
