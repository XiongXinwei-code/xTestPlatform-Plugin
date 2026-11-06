using ZlgEtherCat.Models;
using ZlgEtherCat.Runtime;
using Xunit;

namespace ZlgEtherCat.Tests;

public sealed class DataCodecUsabilityTests
{
    [Theory]
    [InlineData(128)]
    [InlineData(-129)]
    public void SignedByteOverflowMustNotWrap(double value)
        => Assert.Throws<OverflowException>(() => ZlgDataCodec.Encode(value, PdoDataType.Int8));

    [Theory]
    [InlineData(1.5)]
    [InlineData(-1.5)]
    public void IntegralAnalogOutputMustNotSilentlyDiscardFraction(double value)
        => Assert.Throws<ArgumentException>(() => ZlgDataCodec.Encode(value, PdoDataType.Int16));

    [Fact]
    public void TypedWritesPreserveUnsigned64BitAndSignedValues()
    {
        const ulong large = 9007199254740993UL;
        Assert.Equal(large, ZlgDataCodec.Decode(ZlgDataCodec.EncodeValue(large, PdoDataType.UInt64), PdoDataType.UInt64));
        Assert.Equal(ulong.MaxValue, ZlgDataCodec.Decode(ZlgDataCodec.EncodeValue(ulong.MaxValue, PdoDataType.UInt64), PdoDataType.UInt64));
        Assert.Equal(long.MinValue, ZlgDataCodec.Decode(ZlgDataCodec.EncodeValue(long.MinValue, PdoDataType.Int64), PdoDataType.Int64));
        Assert.Equal(new byte[] { 0xFF, 0xFF }, ZlgDataCodec.EncodeValue(-1, PdoDataType.Int16));
        Assert.Equal(new byte[] { 0x80 }, ZlgDataCodec.EncodeValue(-128, PdoDataType.Int8));
        Assert.Equal(new byte[] { 0x34, 0x12 }, ZlgDataCodec.EncodeValue(0x1234, PdoDataType.UInt16));
    }

    [Fact]
    public void TypedWritesRejectAmbiguousAndInvalidInputs()
    {
        Assert.Throws<ArgumentException>(() => ZlgDataCodec.EncodeValue("123", PdoDataType.UInt16));
        Assert.Throws<ArgumentException>(() => ZlgDataCodec.EncodeValue(true, PdoDataType.UInt8));
        Assert.Throws<ArgumentException>(() => ZlgDataCodec.EncodeValue(1, PdoDataType.Boolean));
        Assert.Throws<OverflowException>(() => ZlgDataCodec.EncodeValue(-1, PdoDataType.UInt16));
        Assert.Throws<OverflowException>(() => ZlgDataCodec.EncodeValue(ulong.MaxValue, PdoDataType.Int64));
        Assert.Throws<ArgumentException>(() => ZlgDataCodec.EncodeValue(double.PositiveInfinity, PdoDataType.Float64));
        Assert.Throws<OverflowException>(() => ZlgDataCodec.EncodeValue(double.MaxValue, PdoDataType.Float32));
        var bytes = new byte[] { 1, 2 };
        var encoded = ZlgDataCodec.EncodeValue(bytes, PdoDataType.Bytes);
        Assert.Equal(bytes, encoded);
        Assert.NotSame(bytes, encoded);
    }

    [Fact]
    public void FloatingPointIntegralConversionMustNotRoundThroughDecimal()
    {
        Assert.Equal(9007199254740992UL, ZlgDataCodec.Decode(ZlgDataCodec.EncodeValue(9007199254740992d, PdoDataType.UInt64), PdoDataType.UInt64));
        Assert.Throws<ArgumentException>(() => ZlgDataCodec.EncodeValue(1.0000000000000002d, PdoDataType.Int32));
        Assert.Throws<ArgumentException>(() => ZlgDataCodec.EncodeValue(1.0000001f, PdoDataType.Int32));
    }
}
