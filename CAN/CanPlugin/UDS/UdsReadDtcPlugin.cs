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
        "读取 ECU 故障码（UDS 服务 0x19）。结果以十六进制字符串存入变量。" +
        "Setting 字段：SubFunction(byte,子功能如0x02=报告DTC及状态,默认0x02), StatusMask(byte,DTC状态掩码,默认0xFF), " +
        "ResultVariable(string,结果变量名,写入类型:string 十六进制DTC数据,可选), " +
        "ConnectionName(string,表达式,已打开的CAN连接名), TxId(string,表达式,请求CAN ID), RxId(string,表达式,响应CAN ID), ResponseTimeoutMs(int,响应超时,默认5000)。";

    public override IStepExecutor CreateExecutor() => new UdsReadDtcExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"ReadDTC SubFunc=0x{s.SubFunction:X2} Mask=0x{s.StatusMask:X2} → {s.ResultVariable}";
    }
}
