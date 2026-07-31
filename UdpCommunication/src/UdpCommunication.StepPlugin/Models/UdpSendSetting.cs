using UdpCommunication.StepPlugin.Protocol;

namespace UdpCommunication.StepPlugin.Models;

public class UdpSendSetting
{
    public string RemoteAddress { get; set; } = "127.0.0.1";
    public int RemotePort { get; set; }
    public string LocalAddress { get; set; } = "127.0.0.1";
    public int LocalPort { get; set; }
    public string RequestData { get; set; } = string.Empty;
    public UdpPacketFormat RequestFormat { get; set; } = UdpPacketFormat.Utf8Text;
}
