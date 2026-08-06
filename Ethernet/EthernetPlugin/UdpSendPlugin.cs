using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class UdpSendPlugin : StepPluginBase<UdpSendSetting>
{
    public override string StepTypeId  => "Ethernet.UdpSend";
    public override string DisplayName => "Ethernet_UdpSend";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description =>
        "通过 UDP 向目标地址发送数据（无连接，每次新建 Socket）。" +
        "Setting 字段：RemoteHost(string,表达式,目标IP,默认\"192.168.1.255\"), " +
        "RemotePort(string,表达式,目标端口,默认\"30490\"), " +
        "LocalPort(int,本机发送端口,0=系统自动分配,默认0), " +
        "Data(string,表达式,发送数据,默认\"01 02 03\"), " +
        "Encoding(枚举,数据编码格式:Hex/Utf8/Ascii,默认Hex), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new UdpSendExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"UDP Send: {s.RemoteHost}:{s.RemotePort} [{s.Encoding}]";
    }
}
