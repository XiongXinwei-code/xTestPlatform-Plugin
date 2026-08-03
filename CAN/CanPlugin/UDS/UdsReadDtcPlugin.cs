using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsReadDtcPlugin : StepPluginBase<UdsReadDtcSetting>
{
    public override string StepTypeId => "UDS.ReadDTC";
    public override string DisplayName => "UDS_ReadDTC";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "读取 ECU 故障码（UDS 服务 0x19）。" +
        "Setting 字段：SubFunction(byte,子功能如0x02), StatusMask(byte,状态掩码), ResultVariable(string,结果变量), " +
        "ConnectionName(string,表达式,CAN连接名), TxId(string,表达式), RxId(string,表达式), ResponseTimeoutMs(int)。";

    public override IStepExecutor CreateExecutor() => new UdsReadDtcExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"ReadDTC SubFunc=0x{s.SubFunction:X2} Mask=0x{s.StatusMask:X2} → {s.ResultVariable}";
    }
}
