using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsDiagSessionPlugin : StepPluginBase<UdsDiagSessionSetting>
{
    public override string StepTypeId => "UDS.DiagSession";
    public override string DisplayName => "UDS_DiagSession";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "切换 ECU 诊断会话模式（UDS 服务 0x10）。" +
        "Setting 字段：SessionType(枚举,Default/Programming/Extended), " +
        "SuppressPositiveResponse(bool,是否抑制正响应), " +
        "ConnectionName(string,表达式,CAN连接名), TxId(string,表达式), RxId(string,表达式), ResponseTimeoutMs(int)。";

    public override IStepExecutor CreateExecutor() => new UdsDiagSessionExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DiagSession → {s.SessionType} (TX={s.TxId}, RX={s.RxId})";
    }
}
