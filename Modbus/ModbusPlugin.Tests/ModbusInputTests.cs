using Modbus.Helpers;
using Modbus.Models;
using Xunit;

namespace Modbus.ContractTests;

public class ModbusInputTests
{
    [Fact]
    public void UndefinedReadFormatCannotSilentlyBecomeRawRegisters()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ModbusDataConverter.ConvertRegisters([1], (ModbusDataFormat)999));
    [Theory]
    [InlineData("2")]
    [InlineData("tru")]
    [InlineData("1,,0")]
    [InlineData("")]
    public void InvalidCoilLiteralCannotBecomeAnOutput(string value)
        => Assert.Throws<FormatException>(() => ModbusDataConverter.ParseCoils(value));

    [Fact]
    public void CoilsSupportOnlyExplicitBooleanLiterals()
        => Assert.Equal([true, false, true, false], ModbusDataConverter.ParseCoils("1,0,TRUE,false"));

    [Theory]
    [InlineData(ModbusDataFormat.Float_AB_CD)]
    [InlineData(ModbusDataFormat.UInt32_AB_CD)]
    [InlineData(ModbusDataFormat.Int32_CD_AB)]
    public void HalfOfA32BitValueMustNotBeSilentlyDropped(ModbusDataFormat format)
        => Assert.Throws<ArgumentException>(() => ModbusDataConverter.ConvertRegisters([1, 2, 3], format));
}
