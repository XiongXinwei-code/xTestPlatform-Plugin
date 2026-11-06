using System.Text;
using System.Xml;
using Xunit;
using ZlgEtherCat.Models;
using ZlgEtherCat.Native;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Tests;

public sealed class EniMappingUsabilityTests
{
    [Fact]
    public void IdenticalModulesHaveSeparateRelativeOffsetsAndPreserveSignedTypes()
    {
        using var stream = Xml("""
            <EtherCATConfig><Config><ProcessImage><Inputs>
              <Variable><Name>站A.Value</Name><DataType>INT</DataType><BitSize>16</BitSize><BitOffs>0</BitOffs></Variable>
              <Variable><Name>站B.Value</Name><DataType>INT</DataType><BitSize>16</BitSize><BitOffs>32</BitOffs></Variable>
            </Inputs></ProcessImage></Config></EtherCATConfig>
            """);
        var layout = EniProcessImageLayout.Read(stream);
        var a = Assert.Single(layout.Describe(new PiRange(0, 2), true));
        var b = Assert.Single(layout.Describe(new PiRange(4, 2), true));
        Assert.Equal(0, a.RelativeBitOffset);
        Assert.Equal(0, b.RelativeBitOffset);
        Assert.Equal(32, b.AbsoluteBitOffset);
        Assert.Equal(PdoDataType.Int16, b.ScalarType);
        Assert.True(layout.ValidateScalar(4, 2, PdoDataType.Int16, true));
        Assert.Throws<ArgumentException>(() => layout.ValidateScalar(4, 2, PdoDataType.UInt16, true));
        Assert.Throws<ArgumentException>(() => layout.ValidateScalar(4, 4, PdoDataType.Int32, true));
    }

    [Fact]
    public void BitPackedFieldsCannotBeTreatedAsWholeByteScalars()
    {
        using var stream = Xml("""
            <EtherCATConfig><ProcessImage><Inputs>
              <Variable><Name>Bit0</Name><DataType>BOOL</DataType><BitSize>1</BitSize><BitOffs>0</BitOffs></Variable>
              <Variable><Name>Bit1</Name><DataType>BOOL</DataType><BitSize>1</BitSize><BitOffs>1</BitOffs></Variable>
            </Inputs></ProcessImage></EtherCATConfig>
            """);
        var layout = EniProcessImageLayout.Read(stream);
        Assert.Throws<ArgumentException>(() => layout.ValidateScalar(0, 1, PdoDataType.Boolean, true));
        Assert.All(layout.Describe(new PiRange(0, 1), true), field => Assert.False(field.ByteAligned));
        Assert.False(layout.ValidateScalar(0, 1, PdoDataType.Bytes, true));
    }

    [Fact]
    public void MissingMetadataIsExplicitAndExternalEntitiesAreRejected()
    {
        using var empty = Xml("<EtherCATConfig />");
        var layout = EniProcessImageLayout.Read(empty);
        Assert.False(layout.HasMetadata);
        Assert.False(layout.ValidateScalar(0, 2, PdoDataType.UInt16, true));
        using var hostile = Xml("<!DOCTYPE x [<!ENTITY value SYSTEM 'file:///missing'>]><x>&value;</x>");
        Assert.Throws<XmlException>(() => EniProcessImageLayout.Read(hostile));
    }

    [Theory]
    [InlineData("UINT", "1")]
    [InlineData("VendorCustom", "0")]
    public void UnsupportedByteOrderOrTypeCannotPassScalarValidation(string type, string byteOrder)
    {
        using var stream = Xml($"<EtherCATConfig><ProcessImage><Inputs><Variable><DataType>{type}</DataType><BitSize>16</BitSize><BitOffs>0</BitOffs><VendorSpecific><ByteOrder>{byteOrder}</ByteOrder></VendorSpecific></Variable></Inputs></ProcessImage></EtherCATConfig>");
        var layout = EniProcessImageLayout.Read(stream);
        Assert.Throws<ArgumentException>(() => layout.ValidateScalar(0, 2, PdoDataType.UInt16, true));
        Assert.False(layout.ValidateScalar(0, 2, PdoDataType.UInt16, false));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("0xFFFFFFFF")]
    public void InvalidOffsetsAreRejectedBeforeAnyHardwareCall(string offset)
    {
        using var stream = Xml($"<EtherCATConfig><ProcessImage><Inputs><Variable><DataType>UINT</DataType><BitSize>16</BitSize><BitOffs>{offset}</BitOffs></Variable></Inputs></ProcessImage></EtherCATConfig>");
        Assert.Throws<InvalidDataException>(() => EniProcessImageLayout.Read(stream));
    }

    [Fact]
    public void OverlappingMetadataCannotIdentifyASingleScalar()
    {
        const string variable = "<Variable><DataType>UINT</DataType><BitSize>16</BitSize><BitOffs>0</BitOffs></Variable>";
        using var stream = Xml($"<EtherCATConfig><ProcessImage><Outputs>{variable}{variable}</Outputs></ProcessImage></EtherCATConfig>");
        var layout = EniProcessImageLayout.Read(stream);
        Assert.Throws<ArgumentException>(() => layout.ValidateScalar(0, 2, PdoDataType.UInt16, false));
    }

    private static MemoryStream Xml(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
