using UdpCommunication.StepPlugin.Protocol;
using Xunit;

namespace UdpCommunication.StepPlugin.Tests;

public sealed class UdpMessageCodecTests
{
    [Theory]
    [InlineData("hello", UdpPacketFormat.Utf8Text, "hello")]
    [InlineData("48 65 6C 6C 6F", UdpPacketFormat.Hexadecimal, "48 65 6C 6C 6F")]
    public void EncodeThenDecode_ReturnsConfiguredRepresentation(
        string input,
        UdpPacketFormat format,
        string expected)
    {
        var bytes = UdpMessageCodec.Encode(input, format);

        Assert.Equal(expected, UdpMessageCodec.Decode(bytes, format));
    }

    [Fact]
    public void Encode_HexWithOddDigits_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() =>
            UdpMessageCodec.Encode("ABC", UdpPacketFormat.Hexadecimal));
    }

    [Theory]
    [InlineData("ACK:42", "ACK:42", UdpReplyMatchMode.Exact, true)]
    [InlineData("ACK:42", "ACK", UdpReplyMatchMode.Contains, true)]
    [InlineData("ACK:42", "NACK", UdpReplyMatchMode.Contains, false)]
    public void IsMatch_UsesConfiguredMode(
        string actual,
        string expected,
        UdpReplyMatchMode mode,
        bool expectedResult)
    {
        Assert.Equal(expectedResult, UdpMessageCodec.IsMatch(actual, expected, mode));
    }
}
