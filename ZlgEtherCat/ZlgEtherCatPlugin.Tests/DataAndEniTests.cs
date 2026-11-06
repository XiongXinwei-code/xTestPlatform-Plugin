using System.Text;
using System.Xml;
using ZlgEtherCat.Models;
using ZlgEtherCat.Runtime;
using Xunit;

namespace ZlgEtherCat.Tests;

public class DataAndEniTests
{
    [Theory]
    [InlineData(250)]
    [InlineData(500)]
    public void EniCycleAndDcAreValidatedWithoutStartingHardware(int cycle)
    {
        using var xml = Xml($"<EtherCATConfig><Cyclic><CycleTime>{cycle}</CycleTime></Cyclic><Slave><DC><CycleTime0>{cycle * 1000}</CycleTime0></DC></Slave></EtherCATConfig>");
        Assert.Equal(new EniConfiguration(cycle, true), EniValidation.Read(xml, cycle, true));
    }
    [Fact]
    public void EniRejectsMismatchedCycleMissingDcAndExternalEntities()
    {
        using var wrong = Xml("<EtherCATConfig><CycleTime>1000</CycleTime></EtherCATConfig>");
        Assert.Throws<ArgumentException>(() => EniValidation.Read(wrong, 500, false));
        using var noDc = Xml("<EtherCATConfig><CycleTime>500</CycleTime></EtherCATConfig>");
        Assert.Throws<ArgumentException>(() => EniValidation.Read(noDc, 500, true));
        using var entity = Xml("<!DOCTYPE x [<!ENTITY secret SYSTEM 'file:///missing'>]><x>&secret;</x>");
        Assert.Throws<XmlException>(() => EniValidation.Read(entity, 500, true));
    }
    [Fact]
    public void PdoDecodingPreservesTypeAndLittleEndianAndRejectsBadWidths()
    {
        Assert.IsType<short>(ZlgDataCodec.Decode([255, 255], PdoDataType.Int16));
        Assert.Equal((short)-1, ZlgDataCodec.Decode([255, 255], PdoDataType.Int16));
        Assert.Equal(0x12345678U, ZlgDataCodec.Decode([0x78, 0x56, 0x34, 0x12], PdoDataType.UInt32));
        Assert.Throws<ArgumentException>(() => ZlgDataCodec.Decode([1], PdoDataType.Int32));
        Assert.Equal(3UL, ZlgDataCodec.Bits([0x80, 0x01], 7, 2));
        Assert.Throws<ArgumentException>(() => ZlgDataCodec.Bits([0xFF], 7, 2));
        Assert.Throws<OverflowException>(() => ZlgDataCodec.Encode(65536, PdoDataType.UInt16));
        Assert.Throws<ArgumentException>(() => ZlgDataCodec.Encode(double.NaN, PdoDataType.Float64));
    }
    [Fact]
    public void BoardAliasesResolvePhysicalSerialAndDuplicateAliasesAreRejected()
    {
        ZlgRuntime.BoardEntry[] boards = [new(0, new("HPCIe-2E", "1", "a", 3, 2, 0)), new(1, new("HPCIe-2E", "1", "b", 4, 2, 0))];
        Assert.Equal("a", ZlgRuntime.SelectBoard(boards, "AL3").Info.SerialNumber);
        Assert.Equal("a", ZlgRuntime.SelectBoard(boards, "NOa").Info.SerialNumber);
        Assert.Equal("a", ZlgRuntime.SelectBoard(boards, "ID0").Info.SerialNumber);
        Assert.Throws<ArgumentException>(() => ZlgRuntime.SelectBoard([boards[0], boards[0] with { Index = 2 }], "AL3"));
    }
    private static MemoryStream Xml(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
