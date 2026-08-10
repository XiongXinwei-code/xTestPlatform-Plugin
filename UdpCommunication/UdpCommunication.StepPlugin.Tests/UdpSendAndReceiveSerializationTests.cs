using UdpCommunication.Models;
using UdpCommunication.Protocol;
using Xunit;

namespace UdpCommunication.Tests;

public sealed class UdpSendAndReceiveSerializationTests
{
    [Fact]
    public void Serializer_RoundTripsAllReceiveAndBaseSettings()
    {
        var original = new UdpSendAndReceiveSetting
        {
            OpenStepAddress = "step-1",
            RemoteAddress = "192.168.1.10", RemotePort = 6000,
            RequestData = "01 02", RequestFormat = UdpPacketFormat.Hexadecimal,
            ReceiveTimeoutMs = 4567, ReplyFormat = UdpPacketFormat.Hexadecimal,
            ExpectedReply = "AA", MatchMode = UdpReplyMatchMode.Contains,
            ResponseVariable = "Locals.UdpReply"
        };
        var serializer = new UdpSendAndReceivePlugin().CreateSerializer();
        var restored = (UdpSendAndReceiveSetting)serializer.Deserialize(serializer.Serialize(original), 1);

        Assert.Equal(original.OpenStepAddress, restored.OpenStepAddress);
        Assert.Equal(original.RemoteAddress, restored.RemoteAddress);
        Assert.Equal(original.RemotePort, restored.RemotePort);
        Assert.Equal(original.RequestData, restored.RequestData);
        Assert.Equal(original.ReceiveTimeoutMs, restored.ReceiveTimeoutMs);
        Assert.Equal(original.ResponseVariable, restored.ResponseVariable);
    }
}
