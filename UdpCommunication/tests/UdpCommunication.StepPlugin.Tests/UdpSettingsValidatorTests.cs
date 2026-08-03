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

        Assert.Equal("\u672c\u5730\u5730\u5740\u6216\u76ee\u6807\u5730\u5740\u4e0d\u662f\u6709\u6548\u7684 IP \u5730\u5740", error);
    }

    [Theory]
    [InlineData("127.0.0.1", "::1", false)]
    [InlineData("::1", "::1", true)]
    public void ValidateEndpoint_LiteralAddresses_RequiresSameAddressFamily(
        string localAddress, string remoteAddress, bool valid)
    {
        var error = UdpSettingsValidator.ValidateEndpoint(
            new UdpEndpointOptions(localAddress, 0, remoteAddress, 9000));

        Assert.Equal(valid, error is null);
    }
}
