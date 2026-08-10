using MessagePack;
using UdpCommunication.Protocol;
using xTestPlatform.Core.Models.StepSettings;

namespace UdpCommunication.Models;

[MessagePackObject(true)]
public class UdpSendSetting
{
    /// <summary>引用的 UDP_Open 步骤地址（运行时根据此地址在 RuntimeData 中查找 Transport）。</summary>
    public string OpenStepAddress { get; set; } = string.Empty;

    /// <summary>目标 IP 地址</summary>
    [ExpressionField]
    public string RemoteAddress { get; set; } = "\"127.0.0.1\"";

    /// <summary>目标端口（1~65535）</summary>
    public int RemotePort { get; set; } = 5000;

    /// <summary>发送报文内容</summary>
    [ExpressionField]
    public string RequestData { get; set; } = "\"\"";

    /// <summary>发送报文格式（Utf8Text / Hexadecimal）</summary>
    public UdpPacketFormat RequestFormat { get; set; } = UdpPacketFormat.Utf8Text;
}
