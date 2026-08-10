using UdpCommunication.Executors;
using UdpCommunication.Models;
using UdpCommunication.Protocol;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication;

public sealed class UdpSendAndReceivePlugin : StepPluginBase<UdpSendAndReceiveSetting>
{
    public override string StepTypeId => "Communication.UdpSendAndReceive";
    public override string DisplayName => "UDP_SendAndReceive";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";

    public override string Description =>
        "通过已打开的 UDP 连接发送数据并等待回复（需要先执行 UDP_Open）。\n" +
        "Setting 字段：OpenStepAddress(string,引用的 UDP_Open 步骤地址)，\n" +
        "RemoteAddress(string,表达式,目标 IP)，RemotePort(int,目标端口)，\n" +
        "RequestFormat(枚举,报文格式:Utf8Text/Hexadecimal)，RequestData(string,表达式,发送内容)，\n" +
        "ReceiveTimeoutMs(int,接收超时ms)，ReplyFormat(枚举,回复格式:Utf8Text/Hexadecimal)，\n" +
        "ExpectedReply(string,表达式,期望回复)，MatchMode(枚举,匹配模式:Exact/Contains)，\n" +
        "ResponseVariable(string,回复变量路径，如 Step.UdpReply)。";

    public override IStepExecutor CreateExecutor() => new UdpSendAndReceiveExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"UDP 收发 → {s.OpenStepAddress} → {s.RemoteAddress}:{s.RemotePort} [{s.RequestFormat}] {UdpExecutionLog.Preview(s.RequestData)}，期望 {s.MatchMode}: {UdpExecutionLog.Preview(s.ExpectedReply)}";
    }
}
