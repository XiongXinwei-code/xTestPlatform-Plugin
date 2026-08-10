using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace VISA;

/// <summary>
/// VISA 等待操作完成插件，发送 *OPC? 并等待仪器返回 1
/// </summary>
public sealed class VisaWaitOpcPlugin : StepPluginBase<VisaWaitOpcSetting>
{
    public override string StepTypeId => "IO.VisaWaitOpc";
    public override string DisplayName => "VISA_WaitOPC";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description =>
        "等待仪器当前操作完成（发送 *OPC? 并等待返回 '1'），用于校准、测量等耗时操作的同步。仪器返回 '1' 表示所有挂起的操作已完成。" +
        "Setting 字段：ConnectionName(string,表达式,已打开的VISA连接标识名), TimeoutMs(int,等待超时ms,0=使用Open时设置的默认IO超时,默认0)。";

    public override IStepExecutor CreateExecutor() => new VisaWaitOpcExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"WaitOPC {s.ConnectionName}";
    }
}
