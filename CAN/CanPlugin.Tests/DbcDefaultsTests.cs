using CAN.Dbc;
using Xunit;

namespace CAN.Tests;

public sealed class DbcDefaultsTests
{
    private static DbcMessage Parse(string signals, string attributes = "", int length = 8) =>
        DbcDatabase.Parse($"BO_ 291 Defaults: {length} ECU\n{signals}\n{attributes}").Find(0x123, false);

    [Fact]
    public void MissingInitialValueMeansRawZeroWithoutApplyingPhysicalRange()
    {
        var message = Parse(" SG_ A : 0|8@1+ (0.5,10) [20|100] \"V\" ECU");
        var signal = Assert.Single(message.Signals);
        Assert.False(signal.HasInitialValue);
        Assert.Equal(0m, signal.InitialRawValue);
        var bytes = message.Encode(new Dictionary<string, DbcSignalValue> { ["A"] = new(signal.InitialRawValue, true) });
        Assert.Equal(10m, Assert.Single(message.Decode(bytes)).PhysicalValue);
    }

    [Theory]
    [InlineData("20")]
    [InlineData("20.0")]
    [InlineData("2e1")]
    public void ExplicitInitialValueIsRawAndScalingIsAppliedOnlyOnDecode(string initial)
    {
        var message = Parse(" SG_ A : 0|8@1+ (0.5,7) [10|100] \"V\" ECU",
            $"BA_ \"GenSigStartValue\" SG_ 291 A {initial};");
        var signal = Assert.Single(message.Signals);
        Assert.True(signal.HasInitialValue);
        Assert.Equal(20m, signal.InitialRawValue);
        var bytes = message.Encode(new Dictionary<string, DbcSignalValue> { ["A"] = new(signal.InitialRawValue, true) });
        Assert.Equal(20, bytes[0]);
        Assert.Equal(17m, Assert.Single(message.Decode(bytes)).PhysicalValue);
    }

    [Fact]
    public void SignedInitialValuePreservesSignedRawValueAndMotorolaEncoding()
    {
        var message = Parse(" SG_ A : 7|16@0- (0.001,0) [-32.768|32.767] \"m/s2\" ECU",
            "BA_ \"GenSigStartValue\" SG_ 291 A -30000;");
        var signal = Assert.Single(message.Signals);
        Assert.Equal(-30000m, signal.InitialRawValue);
        var bytes = message.Encode(new Dictionary<string, DbcSignalValue> { ["A"] = new(signal.InitialRawValue, true) });
        Assert.Equal(new byte[] { 0x8A, 0xD0 }, bytes[..2]);
        Assert.Equal(-30m, Assert.Single(message.Decode(bytes)).PhysicalValue);
    }

    [Theory]
    [InlineData("INT")]
    [InlineData("HEX")]
    [InlineData("FLOAT")]
    public void AttributeDefaultAppliesToMissingInitialValuesAndExplicitValueOverrides(string type)
    {
        var message = Parse(" SG_ A : 0|8@1+ (1,0) [0|255] \"\" ECU\n SG_ B : 8|8@1+ (1,0) [0|255] \"\" ECU",
            $"BA_DEF_ SG_ \"GenSigStartValue\" {type} 0 255;\nBA_DEF_DEF_ \"GenSigStartValue\" 9;\nBA_ \"GenSigStartValue\" SG_ 291 B 4;");
        Assert.All(message.Signals, signal => Assert.True(signal.HasInitialValue));
        Assert.Equal(9m, message.Signals[0].InitialRawValue);
        Assert.Equal(4m, message.Signals[1].InitialRawValue);
    }

    [Fact]
    public void InitialValuesDoNotRelaxStrictEncoderAndInactiveBranchIsNotEncoded()
    {
        var message = Parse(" SG_ Selector M : 0|8@1+ (1,0) [0|255] \"\" ECU\n"
            + " SG_ A m1 : 8|8@1+ (1,0) [0|255] \"\" ECU\n SG_ B m2 : 8|8@1+ (1,0) [0|255] \"\" ECU",
            "BA_ \"GenSigStartValue\" SG_ 291 Selector 2;\nBA_ \"GenSigStartValue\" SG_ 291 A 45;\nBA_ \"GenSigStartValue\" SG_ 291 B 67;");
        Assert.Throws<ArgumentException>(() => message.Encode(new Dictionary<string, DbcSignalValue>()));
        var active = message.GetActiveSignals(message.MultiplexerSignal!.InitialRawValue);
        Assert.Equal(new[] { "Selector", "B" }, active.Select(s => s.Name));
        var bytes = message.Encode(active.ToDictionary(s => s.Name, s => new DbcSignalValue(s.InitialRawValue, true)));
        Assert.Equal(new byte[] { 2, 67 }, bytes[..2]);
    }

    [Theory]
    [InlineData("+", "18446744073709551615", "FF-FF-FF-FF-FF-FF-FF-FF")]
    [InlineData("-", "-9223372036854775808", "00-00-00-00-00-00-00-80")]
    [InlineData("-", "9223372036854775807", "FF-FF-FF-FF-FF-FF-FF-7F")]
    public void SixtyFourBitInitialValuesRemainExact(string sign, string initial, string expected)
    {
        var message = Parse($" SG_ A : 0|64@1{sign} (1,0) [0|0] \"\" ECU",
            $"BA_ \"GenSigStartValue\" SG_ 291 A {initial};");
        var signal = Assert.Single(message.Signals);
        var bytes = message.Encode(new Dictionary<string, DbcSignalValue> { ["A"] = new(signal.InitialRawValue, true) });
        Assert.Equal(expected, BitConverter.ToString(bytes));
        Assert.Equal(signal.InitialRawValue, Assert.Single(message.Decode(bytes)).RawValue);
    }

    [Theory]
    [InlineData("BA_ \"GenSigStartValue\" SG_ 999 A 1;")]
    [InlineData("BA_ \"GenSigStartValue\" SG_ 291 Missing 1;")]
    [InlineData("BA_ \"GenSigStartValue\" BO_ 291 1;")]
    [InlineData("BA_ \"GenSigStartValue\"SG_ 291 A 1;")]
    [InlineData("BA_ \"GenSigStartValue\" SG_ 291 A 1.5;")]
    [InlineData("BA_ \"GenSigStartValue\" SG_ 291 A \"1\";")]
    [InlineData("BA_ \"GenSigStartValue\" SG_ 291 A 256;")]
    [InlineData("BA_ \"GenSigStartValue\" SG_ 291 A -1;")]
    [InlineData("BA_ \"GenSigStartValue\" SG_ 291 A 1; BA_ \"GenSigStartValue\" SG_ 291 A 2;")]
    [InlineData("BA_DEF_DEF_ \"GenSigStartValue\" 256;")]
    [InlineData("BA_DEF_DEF_ \"GenSigStartValue\" 0.25;")]
    [InlineData("BA_DEF_DEF_ \"GenSigStartValue\" 0; BA_DEF_DEF_ \"GenSigStartValue\" 1;")]
    [InlineData("BA_DEF_ SG_ \"GenSigStartValue\" STRING;")]
    [InlineData("BA_DEF_ BO_ \"GenSigStartValue\" INT 0 255;")]
    [InlineData("BA_DEF_ SG_ \"GenSigStartValue\" INT 255 0;")]
    public void InvalidInitialMetadataIsRejectedBeforeAnyFrameCanBeEncoded(string attributes)
    {
        Assert.Throws<FormatException>(() => Parse(" SG_ A : 0|8@1+ (1,0) [0|255] \"\" ECU", attributes));
    }

    [Theory]
    [InlineData("-", "18446744073709551615")]
    [InlineData("-", "9223372036854775808")]
    [InlineData("-", "-9223372036854775809")]
    [InlineData("+", "18446744073709551616")]
    public void OutOfRangeSixtyFourBitValuesAreNotTruncatedOrReinterpreted(string sign, string initial)
    {
        Assert.Throws<FormatException>(() => Parse($" SG_ A : 0|64@1{sign} (1,0) [0|0] \"\" ECU",
            $"BA_ \"GenSigStartValue\" SG_ 291 A {initial};"));
    }

    [Fact]
    public void ExplicitValueMayOverrideAnOtherwiseOutOfRangeAttributeDefault()
    {
        var message = Parse(" SG_ A : 0|4@1+ (1,0) [0|15] \"\" ECU",
            "BA_DEF_DEF_ \"GenSigStartValue\" 255; BA_ \"GenSigStartValue\" SG_ 291 A 3;");
        Assert.Equal(3m, Assert.Single(message.Signals).InitialRawValue);
    }

    [Fact]
    public void ExtendedMessageInitialMetadataUsesOriginalDbcIdentifier()
    {
        var message = DbcDatabase.Parse("BO_ 2147483939 Extended: 1 ECU\n SG_ A : 0|8@1+ (1,0) [0|255] \"\" ECU\n"
            + "BA_ \"GenSigStartValue\" SG_ 2147483939 A 17;").Find(0x123, true);
        Assert.Equal(17m, Assert.Single(message.Signals).InitialRawValue);
    }

    [Fact]
    public void ExplicitZeroIsDistinguishableFromMissingValueAndUnrelatedStringAttributesAreIgnored()
    {
        var message = Parse(" SG_ A : 0|8@1+ (1,0) [0|255] \"\" ECU",
            "BA_DEF_ SG_ \"Note\" ENUM \"GenSigStartValue\";\n"
            + "BA_DEF_DEF_ \"Note\" \"GenSigStartValue\";\n"
            + "BA_ \"Note\" SG_ 291 A \"GenSigStartValue\";\n"
            + "BA_ \"GenSigStartValue\" SG_ 291 A 0;");
        var signal = Assert.Single(message.Signals);
        Assert.True(signal.HasInitialValue);
        Assert.Equal(0m, signal.InitialRawValue);
    }
}
