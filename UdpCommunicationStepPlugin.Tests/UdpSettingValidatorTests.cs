using UdpCommunicationStepPlugin.Setting;
using Xunit;

namespace UdpCommunicationStepPlugin.Tests;

public sealed class UdpSettingValidatorTests
{
    [Theory]
    [InlineData("", 5000, "UDP_001")]
    [InlineData("127.0.0.1", 0, "UDP_002")]
    public void Validate_reports_invalid_endpoint(string host, int remotePort, string code)
    {
        var issues = UdpSettingValidator.Validate(new UdpCommunicationSetting { RemoteHost = host, RemotePort = remotePort });

        Assert.Contains(issues, issue => issue.Code == code);
    }
}
