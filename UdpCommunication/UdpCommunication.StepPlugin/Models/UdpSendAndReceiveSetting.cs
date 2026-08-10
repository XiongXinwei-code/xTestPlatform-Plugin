using MessagePack;
using UdpCommunication.Protocol;
using xTestPlatform.Core.Models.StepSettings;

namespace UdpCommunication.Models;

[MessagePackObject(true)]
public class UdpSendAndReceiveSetting : UdpSendSetting
{
    /// <summary>接收超时（毫秒）</summary>
    public int ReceiveTimeoutMs { get; set; } = 3000;

    /// <summary>回复报文格式（Utf8Text / Hexadecimal）</summary>
    public UdpPacketFormat ReplyFormat { get; set; } = UdpPacketFormat.Utf8Text;

    /// <summary>期望回复内容</summary>
    [ExpressionField]
    public string ExpectedReply { get; set; } = "\"\"";

    /// <summary>匹配模式（Exact / Contains）</summary>
    public UdpReplyMatchMode MatchMode { get; set; } = UdpReplyMatchMode.Exact;

    /// <summary>回复变量路径（如 Step.UdpReply）</summary>
    public string ResponseVariable { get; set; } = "Step.UdpReply";
}
