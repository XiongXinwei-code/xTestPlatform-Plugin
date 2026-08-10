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
        "打开 VISA 仪器会话，支持 GPIB、USB-TMC、TCP/LAN(SOCKET/INSTR)、串口等资源。打开后通过 ConnectionName 标识此连接，供后续 Write/Read/Query 步骤使用。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名,默认VISA1), ResourceString(string,表达式,VISA资源字符串,如TCPIP::192.168.1.1::INSTR或GPIB0::1::INSTR), " +
        "OpenTimeoutMs(int,打开超时ms,默认5000), IoTimeoutMs(int,IO超时ms,默认3000), Terminator(string,终止符,默认\\n)。";

    public override IStepExecutor CreateExecutor() => new VisaOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Open {s.ConnectionName} ({s.ResourceString})";
    }
}
