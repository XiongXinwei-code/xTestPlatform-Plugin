using MessagePack;
using UdpCommunication.StepPlugin.Protocol;

namespace UdpCommunication.StepPlugin.Models;

[MessagePackObject(true)]
public sealed class UdpSendAndReceiveSetting : UdpSendSetting
{
    public int ReceiveTimeoutMs { get; set; } = 3000;
    public UdpPacketFormat ReplyFormat { get; set; } = UdpPacketFormat.Utf8Text;
    public string ExpectedReply { get; set; } = string.Empty;
    public UdpReplyMatchMode MatchMode { get; set; } = UdpReplyMatchMode.Exact;
    public string ResponseVariable { get; set; } = string.Empty;
}
