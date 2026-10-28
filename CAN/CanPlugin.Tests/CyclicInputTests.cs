using CAN.CANopen.Cyclic;
using System.Text.Json;
using Xunit;

namespace CAN.Tests;

public class CyclicInputTests
{
    [Fact]
    public void JsonKeepsRawBytesAndRejectsUnknownFields()
    {
        var table = CyclicInputParser.Trajectory("""[{"OffsetMs":0,"Frames":[{"CobId":513,"DataHex":"01 00 FF 7F"}]}]""", 100);
        Assert.Equal(new byte[] { 1, 0, 255, 127 }, table.Points[0].Frames[0].Data);
        Assert.Throws<JsonException>(() => CyclicInputParser.Trajectory("""[{"OffsetMs":0,"Framez":[]}]""", 100));
        Assert.Null(CyclicInputParser.Stop("[]", 100));
        Assert.Throws<ArgumentException>(() => CyclicInputParser.Trajectory(new string(' ', 8_000_001), 100));
    }
}
