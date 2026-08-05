using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqDiReadPlugin : StepPluginBase<NiDaqDiReadSetting>
{
    public override string StepTypeId => "NiDaq.DiRead";
    public override string DisplayName => "NiDaq_DI_Read";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "读取 NI DAQ 数字输入通道的状态值。" +
        "Setting 字段：Channel(string,表达式,物理通道如Dev1/port0/line0:7), ResultVariable(string,结果变量名,写入类型:uint 端口状态值)。";

    public override IStepExecutor CreateExecutor() => new NiDaqDiReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DI Read: {s.Channel} → {s.ResultVariable}";
    }
}
