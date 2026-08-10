using UdpCommunication.Executors;
using UdpCommunication.Models;
using UdpCommunication.Protocol;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication;

public sealed class UdpSendPlugin : StepPluginBase<UdpSendSetting>
{
    public override string StepTypeId => "Communication.UdpSend";
    public override string DisplayName => "UDP_Send";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";

    public override string Description =>
        "通过已打开的 UDP 连接发送数据（需要先执行 UDP_Open）。\n" +
        "Setting 字段：OpenStepAddress(string,引用的 UDP_Open 步骤地址)，\n" +
        "RemoteAddress(string,表达式,目标 IP)，RemotePort(int,目标端口)，\n" +
        "RequestFormat(枚举,报文格式:Utf8Text/Hexadecimal)，RequestData(string,表达式,发送内容)。";

    public override IStepExecutor CreateExecutor() => new UdpSendExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"UDP 发送 → {s.OpenStepAddress} → {s.RemoteAddress}:{s.RemotePort} [{s.RequestFormat}] {UdpExecutionLog.Preview(s.RequestData)}";
    }
}
