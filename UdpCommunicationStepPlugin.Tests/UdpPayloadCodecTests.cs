using UdpCommunicationStepPlugin.Infrastructure;
using UdpCommunicationStepPlugin.Setting;
using Xunit;

namespace UdpCommunicationStepPlugin.Tests;

public sealed class UdpPayloadCodecTests
{
    [Theory]
    [InlineData("PING", UdpDataFormat.Utf8Text, "50494E47")]
    [InlineData("50 49 4e 47", UdpDataFormat.Hex, "50494E47")]
    public void Encode_returns_expected_bytes(string payload, UdpDataFormat format, string expectedHex)
    {
        var bytes = UdpPayloadCodec.Encode(payload, format);

        Assert.Equal(expectedHex, Convert.ToHexString(bytes));
    }

    [Theory]
    [InlineData("504F4E47", UdpDataFormat.Utf8Text, "PONG")]
    [InlineData("504F4E47", UdpDataFormat.Hex, "504F4E47")]
    public void Decode_returns_normalized_data(string hex, UdpDataFormat format, string expected)
    {
        var value = UdpPayloadCodec.Decode(Convert.FromHexString(hex), format);

        Assert.Equal(expected, value);
    }

    [Fact]
    public void Encode_rejects_odd_length_hex_payload()
    {
        Assert.Throws<FormatException>(() => UdpPayloadCodec.Encode("ABC", UdpDataFormat.Hex));
    }
}
