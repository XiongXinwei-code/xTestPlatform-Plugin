using MessagePack;

namespace UdpCommunicationStepPlugin.Setting;

[MessagePackObject(true)]
public sealed class UdpCommunicationSetting
{
    public string RemoteHost { get; set; } = "127.0.0.1";

    public int RemotePort { get; set; } = 5000;

    public int LocalPort { get; set; }

    public UdpOperationMode OperationMode { get; set; } = UdpOperationMode.SendOnly;

    public UdpDataFormat DataFormat { get; set; } = UdpDataFormat.Utf8Text;

    public string Payload { get; set; } = string.Empty;

    public int ResponseTimeoutMs { get; set; } = 3000;

    public UdpResponseMatchMode ResponseMatchMode { get; set; } = UdpResponseMatchMode.AnyResponse;

    public string ExpectedResponse { get; set; } = string.Empty;

    public string ResponseVariableName { get; set; } = "UdpResponse";
}
