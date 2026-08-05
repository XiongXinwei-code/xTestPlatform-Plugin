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
        "从已配置的编码器任务中读取当前位置值，存入指定变量。" +
        "Setting 字段：TaskName(string,表达式,要读取的编码器任务名), ReadTimeoutMs(int,读取超时ms,默认5000), ResultVariable(string,结果变量名,写入类型:double 位置值)。";

    public override IStepExecutor CreateExecutor() => new NiDaqEncoderReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Encoder Read: {s.TaskName} → {s.ResultVariable}";
    }
}
