using UdpCommunication.StepPlugin.Transport;
using UdpCommunication.StepPlugin.Validation;
using Xunit;

namespace UdpCommunication.StepPlugin.Tests;

public sealed class UdpSettingsValidatorTests
{
    [Fact]
    public void ValidateEndpoint_InvalidLocalAddress_ReturnsChineseError()
    {
        var error = UdpSettingsValidator.ValidateEndpoint(new UdpEndpointOptions("bad-ip", 0, "127.0.0.1", 9000));

        Assert.Equal("本地地址或目标地址不是有效的 IPv4 地址", error);
    }
}
