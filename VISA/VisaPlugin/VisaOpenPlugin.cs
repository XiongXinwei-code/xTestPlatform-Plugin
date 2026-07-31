using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace VISA;

/// <summary>
/// VISA 打开会话插件，通过 Resource String 建立与仪器的连接
/// </summary>
public sealed class VisaOpenPlugin : StepPluginBase<VisaOpenSetting>
{
    public override string StepTypeId => "IO.VisaOpen";
    public override string DisplayName => "VISA_Open";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description =>
        "打开 VISA 仪器会话，支持 GPIB、USB-TMC、TCP/LAN(SOCKET/INSTR)、串口等资源。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名), ResourceString(string,表达式,VISA资源字符串), " +
        "OpenTimeoutMs(int,打开超时ms), IoTimeoutMs(int,IO超时ms), Terminator(string,终止符)。";

    public override IStepExecutor CreateExecutor() => new VisaOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Open {s.ConnectionName} ({s.ResourceString})";
    }
}
