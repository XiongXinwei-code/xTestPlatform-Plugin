using System.Globalization;
using Opc.Ua;
using OpcUa.Helpers;
using OpcUa.Models;
using Xunit;

namespace OpcUa.Tests;

public sealed class InvariantConditionTests
{
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void NumericConditionsUseProtocolFormatInsteadOfWorkstationCulture(string name)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            var value = new DataValue(new Variant(1.5));
            Assert.False(OpcUaHelper.CompareValue(value, "2", OpcUaCompareMode.GreaterThan));
            Assert.True(OpcUaHelper.CompareValue(value, "2", OpcUaCompareMode.LessThan));
            Assert.False(OpcUaHelper.CompareValue("1,5", "2", OpcUaCompareMode.GreaterThan));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1,000.5")]
    [InlineData("1,5")]
    public void FloatingInputsRejectNonFiniteAndThousands(string text)
    {
        Assert.Throws<FormatException>(() => OpcUaHelper.ConvertValue(text, OpcUaDataType.Float));
        Assert.Throws<FormatException>(() => OpcUaHelper.ConvertValue(text, OpcUaDataType.Double));
        Assert.False(OpcUaHelper.CompareValue(text, "2", OpcUaCompareMode.GreaterThan));
        Assert.False(OpcUaHelper.CompareValue("2", text, OpcUaCompareMode.LessThan));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void WriteValueConversionUsesInvariantDecimalSeparator(string name)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            Assert.Equal(1.5f, OpcUaHelper.ConvertValue("1.5", OpcUaDataType.Float));
            Assert.Equal(1.5, OpcUaHelper.ConvertValue("1.5", OpcUaDataType.Double));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
