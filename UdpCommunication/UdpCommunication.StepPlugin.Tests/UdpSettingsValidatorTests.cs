using UdpCommunication.Transport;
using UdpCommunication.Validation;
using Xunit;

namespace UdpCommunication.Tests;

public sealed class UdpSettingsValidatorTests
{
    [Fact]
    public void ValidateEndpoint_InvalidLocalAddress_ReturnsChineseError()
    {
        var error = UdpSettingsValidator.ValidateEndpoint(new UdpEndpointOptions("bad-ip", 0, "127.0.0.1", 9000));

        Assert.Equal("本地地址或目标地址不是有效的 IP 地址", error);
    }

    [Theory]
    [InlineData("127.0.0.1", "::1", false)]
    [InlineData("::1", "::1", true)]
    public void ValidateEndpoint_LiteralAddresses_RequiresSameAddressFamily(
        string localAddress, string remoteAddress, bool addressFamilyValid)
    {
        // LocalPort 固定为 5001（1~65535 范围内）：新策略禁止 LocalPort=0 走入核心校验逻辑。
        // 该测试仅验证 AddressFamily 匹配规则，因此端口需要改为有效值。
        var error = UdpSettingsValidator.ValidateEndpoint(
            new UdpEndpointOptions(localAddress, 5001, remoteAddress, 9000));

        Assert.Equal(addressFamilyValid, error is null);
    }
}
