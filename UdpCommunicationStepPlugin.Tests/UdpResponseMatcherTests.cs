using UdpCommunicationStepPlugin.Infrastructure;
using UdpCommunicationStepPlugin.Setting;
using Xunit;

namespace UdpCommunicationStepPlugin.Tests;

public sealed class UdpResponseMatcherTests
{
    [Theory]
    [InlineData("PONG", "ignored", UdpResponseMatchMode.AnyResponse, true)]
    [InlineData("PONG", "PONG", UdpResponseMatchMode.Exact, true)]
    [InlineData("PONG", "PON", UdpResponseMatchMode.Contains, true)]
    [InlineData("PONG", "PING", UdpResponseMatchMode.Exact, false)]
    [InlineData("PONG", "PING", UdpResponseMatchMode.Contains, false)]
    public void IsMatch_implements_configured_strategy(
        string actual,
        string expected,
        UdpResponseMatchMode mode,
        bool expectedResult)
    {
        var result = UdpResponseMatcher.IsMatch(actual, expected, mode);

        Assert.Equal(expectedResult, result);
    }
}
