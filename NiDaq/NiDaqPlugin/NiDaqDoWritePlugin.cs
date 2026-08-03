using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqDoWritePlugin : StepPluginBase<NiDaqDoWriteSetting>
{
    public override string StepTypeId => "NiDaq.DoWrite";
    public override string DisplayName => "NiDaq_DO_Write";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "设置 NI DAQ 数字输出通道的状态值。" +
        "Setting 字段：Channel(string,表达式,物理通道如Dev1/port0/line0), Value(string,表达式,输出值true/false或byte)。";

    public override IStepExecutor CreateExecutor() => new NiDaqDoWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DO Write: {s.Channel} = {s.Value}";
    }
}
