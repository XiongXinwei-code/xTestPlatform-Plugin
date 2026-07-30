using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsClearDtcPlugin : StepPluginBase<UdsClearDtcSetting>
{
    public override string StepTypeId => "UDS.ClearDTC";
    public override string DisplayName => "UDS_ClearDTC";
    public override string Category => "UDS";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "清除 ECU 故障码（UDS 服务 0x14）。" +
        "Setting 字段：DtcGroup(string,DTC组 0xFFFFFF=全部清除)。";

    public override IStepExecutor CreateExecutor() => new UdsClearDtcExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"ClearDTC Group={s.DtcGroup}";
    }
}
