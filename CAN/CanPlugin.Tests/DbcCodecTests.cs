using CAN.Dbc;
using Xunit;

namespace CAN.Tests;

public sealed class DbcCodecTests
{
    private static DbcMessage Parse(string signals, int length = 8, string tail = "") =>
        DbcDatabase.Parse($"BO_ 291 Test: {length} ECU\n{signals}\n{tail}").Find(0x123, false);

    [Fact]
    public void Intel_and_motorola_match_independent_byte_fixture()
    {
        var message = Parse("""
             SG_ Le : 0|12@1+ (0.5,-10) [0|2000] "V" ECU
             SG_ Be : 23|12@0- (1,0) [-2048|2047] "A" ECU
            """);
        var data = message.Encode(new Dictionary<string, DbcSignalValue> { ["Le"] = new(1350), ["Be"] = new(-2) });
        Assert.Equal(new byte[] { 0xA0, 0x0A, 0xFF, 0xE0, 0, 0, 0, 0 }, data);
        var values = message.Decode(data);
        Assert.Equal(2720m, values.Single(x => x.Name == "Le").RawValue);
        Assert.Equal(1350m, values.Single(x => x.Name == "Le").PhysicalValue);
        Assert.Equal(-2m, values.Single(x => x.Name == "Be").RawValue);
    }

    [Theory]
    [InlineData("2|5@0+", 21, 0x05, 0x40)]
    [InlineData("2|9@1+", 257, 0x04, 0x04)]
    [InlineData("7|1@1-", -1, 0x80, 0x00)]
    public void Non_aligned_and_single_bit_layouts_match_known_bytes(string layout, int raw, byte first, byte second)
    {
        var message = Parse($" SG_ V : {layout} (1,0) [0|0] \"\" ECU", 2);
        Assert.Equal(new byte[] { first, second }, message.Encode(new Dictionary<string, DbcSignalValue> { ["V"] = new(raw, true) }));
        Assert.Equal(raw, Assert.Single(message.Decode([first, second])).RawValue);
    }

    [Fact]
    public void Multiplexing_allows_same_bits_in_different_branches_and_only_encodes_active_values()
    {
        var message = Parse("""
             SG_ Mode M : 0|4@1+ (1,0) [0|15] "" ECU
             SG_ Common : 4|4@1+ (1,0) [0|15] "" ECU
             SG_ A m1 : 8|8@1+ (1,0) [0|255] "" ECU
             SG_ B m2 : 8|8@1+ (2,0) [0|510] "" ECU
            """);
        var bytes = message.Encode(new Dictionary<string, DbcSignalValue> { ["Mode"] = new(2), ["Common"] = new(3), ["B"] = new(100) });
        Assert.Equal(new byte[] { 0x32, 0x32, 0, 0, 0, 0, 0, 0 }, bytes);
        Assert.Equal(new[] { "Mode", "Common", "B" }, message.Decode(bytes).Select(x => x.Name));
        Assert.Equal(100m, message.Decode(bytes).Single(x => x.Name == "B").PhysicalValue);
    }

    [Fact]
    public void Extended_id_and_fd_metadata_are_independent_of_payload_length()
    {
        var db = DbcDatabase.Parse("""
            BO_ 291 Std: 8 ECU
             SG_ V : 0|8@1+ (1,0) [0|255] "" ECU
            BO_ 2147483939 Ext: 8 ECU
             SG_ V : 0|8@1+ (1,0) [0|255] "" ECU
            BA_DEF_ BO_ "VFrameFormat" ENUM "StandardCAN","ExtendedCAN","reserved","J1939PG","reserved","reserved","reserved","reserved","reserved","reserved","reserved","reserved","reserved","reserved","StandardCAN_FD","ExtendedCAN_FD";
            BA_ "VFrameFormat" BO_ 2147483939 15;
            """);
        Assert.False(db.Find(0x123, false).IsFd); Assert.True(db.Find(0x123, true).IsFd);
    }

    [Fact]
    public void Sixty_four_bit_integer_values_do_not_round_through_double()
    {
        var unsigned = Parse(" SG_ U : 0|64@1+ (1,0) [0|0] \"\" ECU");
        var bytes = unsigned.Encode(new Dictionary<string, DbcSignalValue> { ["U"] = new(ulong.MaxValue, true) });
        Assert.All(bytes, b => Assert.Equal(255, b)); Assert.Equal((decimal)ulong.MaxValue, Assert.Single(unsigned.Decode(bytes)).RawValue);
        var signed = Parse(" SG_ S : 7|64@0- (1,0) [0|0] \"\" ECU");
        bytes = signed.Encode(new Dictionary<string, DbcSignalValue> { ["S"] = new(long.MinValue, true) });
        Assert.Equal(new byte[] { 0x80, 0, 0, 0, 0, 0, 0, 0 }, bytes); Assert.Equal((decimal)long.MinValue, Assert.Single(signed.Decode(bytes)).RawValue);
    }

    [Theory]
    [InlineData("SIG_VALTYPE_ 291 V : 1;")]
    [InlineData("SG_MUL_VAL_ 291 V Mode 1-3;")]
    public void Unsupported_layouts_are_explicitly_rejected(string extra)
        => Assert.Throws<NotSupportedException>(() => Parse(" SG_ V : 0|8@1+ (1,0) [0|255] \"\" ECU", tail: extra));

    [Fact]
    public void Comments_enumerations_and_symbol_headers_do_not_turn_into_signal_definitions()
    {
        var db = DbcDatabase.Parse("""
            VERSION ""
            NS_ :
                CM_
                SIG_VALTYPE_
                SG_MUL_VAL_
            BS_:
            BU_: ECU
            BO_ 291 Msg: 2 ECU
             SG_ State : 0|8@1+ (1,0) [0|255] "状态" ECU
            CM_ SG_ 291 State "第一行;
            第二行包含\"文字\"";
            VAL_ 291 State 0 "停止" 1 "运行";
            VAL_ Environment 0 "未用";
            SIG_VALTYPE_ 291 State : 0;
            """);
        var message = db.Find(291, false); var signal = Assert.Single(message.Signals);
        Assert.Contains("第二行包含\"文字\"", signal.Comment);
        var value = Assert.Single(message.Decode([1, 0])); Assert.Equal("运行", value.ValueDescription); Assert.Equal("状态", value.Unit);
    }

    [Fact]
    public void Fd_lengths_and_byte_63_are_supported_and_padding_is_not_decoded_as_extra_signals()
    {
        var message = Parse(" SG_ Tail : 504|8@1+ (1,0) [0|255] \"\" ECU", 64);
        Assert.True(message.IsFd);
        var bytes = message.Encode(new Dictionary<string, DbcSignalValue> { ["Tail"] = new(0xA5) });
        Assert.Equal(64, bytes.Length); Assert.Equal(0xA5, bytes[63]); Assert.All(bytes.Take(63), b => Assert.Equal(0, b));
        Assert.Throws<ArgumentException>(() => message.Decode(new byte[63]));
        var shortMessage = Parse(" SG_ V : 0|8@1+ (1,0) [0|255] \"\" ECU", 9);
        Assert.Equal(12m, Assert.Single(shortMessage.Decode([12, 0, 0, 0, 0, 0, 0, 0, 0, 99, 99, 99])).RawValue);
    }

    [Fact]
    public void Negative_scaling_and_unset_ranges_work_but_inexact_values_do_not_round()
    {
        var message = Parse(" SG_ V : 0|8@1- (-0.5,10) [-20|20] \"V\" ECU");
        Assert.Equal(6, message.Encode(new Dictionary<string, DbcSignalValue> { ["V"] = new(7) })[0]);
        Assert.Throws<ArgumentException>(() => message.Encode(new Dictionary<string, DbcSignalValue> { ["V"] = new(7.1m) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => message.Encode(new Dictionary<string, DbcSignalValue> { ["V"] = new(21) }));
        Assert.Equal(0x80, message.Encode(new Dictionary<string, DbcSignalValue> { ["V"] = new(-128, true) })[0]);
        var unset = Parse(" SG_ V : 0|8@1+ (2,3) [0|0] \"\" ECU");
        Assert.Equal(4, unset.Encode(new Dictionary<string, DbcSignalValue> { ["V"] = new(11) })[0]);
    }

    [Theory]
    [InlineData(" SG_ A : 0|8@1+ (1,0) [0|0] \"\" ECU\n SG_ B : 7|8@0+ (1,0) [0|0] \"\" ECU")]
    [InlineData(" SG_ M M : 0|8@1+ (1,0) [0|0] \"\" ECU\n SG_ A m1 : 8|8@1+ (1,0) [0|0] \"\" ECU\n SG_ B m1 : 8|8@1+ (1,0) [0|0] \"\" ECU")]
    [InlineData(" SG_ A m1 : 8|8@1+ (1,0) [0|0] \"\" ECU")]
    [InlineData(" SG_ A M : 0|8@1+ (1,0) [0|0] \"\" ECU\n SG_ B M : 8|8@1+ (1,0) [0|0] \"\" ECU")]
    [InlineData(" SG_ A : 63|2@1+ (1,0) [0|0] \"\" ECU")]
    [InlineData(" SG_ A : 56|2@0+ (1,0) [0|0] \"\" ECU")]
    [InlineData(" SG_ A : 0|65@1+ (1,0) [0|0] \"\" ECU")]
    [InlineData(" SG_ A : 0|8@1+ (0,0) [0|0] \"\" ECU")]
    [InlineData(" SG_ A : 0|8@1+ (1,0) [10|1] \"\" ECU")]
    public void Invalid_or_overlapping_layouts_are_rejected(string signals)
        => Assert.Throws<FormatException>(() => Parse(signals));

    [Fact]
    public void Active_signal_inputs_are_required_but_inactive_inputs_are_not_evaluated_by_codec()
    {
        var message = Parse("""
             SG_ Mode M : 0|8@1+ (2,0) [0|0] "" ECU
             SG_ A m1 : 8|8@1+ (1,0) [0|10] "" ECU
             SG_ B m2 : 8|8@1+ (1,0) [0|10] "" ECU
            """);
        var values = new Dictionary<string, DbcSignalValue> { ["Mode"] = new(2), ["A"] = new(5), ["B"] = new(9999) };
        Assert.Equal(new byte[] { 1, 5, 0, 0, 0, 0, 0, 0 }, message.Encode(values));
        values.Remove("A"); Assert.Throws<ArgumentException>(() => message.Encode(values));
        values["A"] = new(5); values["Unknown"] = new(0); Assert.Throws<ArgumentException>(() => message.Encode(values));
        Assert.Equal(new[] { "Mode", "B" }, message.GetActiveSignals(2).Select(s => s.Name));
    }

    [Fact]
    public void Global_fd_marker_and_custom_enum_index_are_honored()
    {
        var marker = Parse(" SG_ V : 0|8@1+ (1,0) [0|255] \"\" ECU", tail: "BA_ \"BusType\" \"CAN FD\";");
        Assert.True(marker.IsFd);
        var custom = Parse(" SG_ V : 0|8@1+ (1,0) [0|255] \"\" ECU", tail: "BA_DEF_ BO_ \"VFrameFormat\" ENUM \"StandardCAN_FD\",\"StandardCAN\";\nBA_ \"VFrameFormat\" BO_ 291 0;");
        Assert.True(custom.IsFd);
        Assert.Throws<FormatException>(() => Parse("", tail: "BA_ \"VFrameFormat\" BO_ 291 1;"));
        Assert.True(Parse("", tail: "BA_DEF_DEF_ \"BusType\" \"CAN FD\";").IsFd);
        Assert.False(Parse("", tail: "BA_ \"BusType\" \"CAN\";\nBA_DEF_DEF_ \"BusType\" \"CAN FD\";").IsFd);
    }

    [Fact]
    public void Vector_independent_signals_are_not_a_real_bus_message()
    {
        var db = DbcDatabase.Parse("""
            BO_ 3221225472 VECTOR__INDEPENDENT_SIG_MSG: 0 Vector__XXX
             SG_ Fake : 0|64@1+ (1,0) [0|0] "" ECU
            SIG_VALTYPE_ 3221225472 Fake : 1;
            CM_ SG_ 3221225472 Fake "伪信号";
            VAL_ 3221225472 Fake 0 "无";
            BA_ "VFrameFormat" BO_ 3221225472 0;
            BO_ 291 Real: 0 ECU
            """);
        Assert.Equal("Real", Assert.Single(db.Messages).Name);
    }

    [Theory]
    [InlineData("0.10000000000000000000000000001")]
    [InlineData("1e-29")]
    [InlineData("79228162514264337593543950336")]
    public void Scaling_that_would_be_silently_rounded_is_rejected(string factor)
        => Assert.Throws<FormatException>(() => Parse($" SG_ V : 0|64@1+ ({factor},0) [0|0] \"\" ECU"));

    [Fact]
    public void Decimal_scientific_scaling_preserves_large_integer_values()
    {
        var message = Parse(" SG_ V : 0|64@1+ (1e-1,0) [0|0] \"\" ECU");
        var values = message.Decode(Enumerable.Repeat((byte)255, 8).ToArray());
        Assert.Equal(1844674407370955161.5m, Assert.Single(values).PhysicalValue);
        Assert.All(message.Encode(new Dictionary<string, DbcSignalValue> { ["V"] = new(1844674407370955161.5m) }), b => Assert.Equal(255, b));
    }

    [Fact]
    public void Scaling_does_not_lose_low_decimal_digits_in_multiply_or_divide()
    {
        var message = Parse(" SG_ V : 0|64@1+ (0.1234567890123456789012345678,0) [0|0] \"\" ECU");
        Assert.Throws<OverflowException>(() => message.Decode(Enumerable.Repeat((byte)255, 8).ToArray()));
        Assert.Throws<ArgumentException>(() => message.Encode(new Dictionary<string, DbcSignalValue> { ["V"] = new(1.234567890123456789012345678m + 0.0000000000000000000000000001m) }));
        Assert.Equal(10, message.Encode(new Dictionary<string, DbcSignalValue> { ["V"] = new(1.234567890123456789012345678m) })[0]);
        Assert.Equal(1.234567890123456789012345678m, Assert.Single(message.Decode([10, 0, 0, 0, 0, 0, 0, 0])).PhysicalValue);
    }

    [Fact]
    public void Exact_scaling_supports_large_intermediate_values_that_cancel_with_offset()
    {
        var message = Parse(" SG_ V : 0|8@1+ (79228162514264337593543950335,-79228162514264337593543950335) [0|0] \"\" ECU");
        Assert.Equal(decimal.MaxValue, Assert.Single(message.Decode([2, 0, 0, 0, 0, 0, 0, 0])).PhysicalValue);
        Assert.Equal(2, message.Encode(new Dictionary<string, DbcSignalValue> { ["V"] = new(decimal.MaxValue) })[0]);
    }

    [Fact]
    public void Signed_multiplexer_uses_raw_value_for_branch_selection()
    {
        var message = Parse("""
             SG_ Mode M : 0|8@1- (2,1) [0|0] "" ECU
             SG_ A m1 : 8|8@1+ (1,0) [0|0] "" ECU
            """);
        var values = new Dictionary<string, DbcSignalValue> { ["Mode"] = new(3), ["A"] = new(42) };
        Assert.Equal(new byte[] { 1, 42, 0, 0, 0, 0, 0, 0 }, message.Encode(values));
        Assert.Equal(new[] { "Mode", "A" }, message.Decode([1, 42, 0, 0, 0, 0, 0, 0]).Select(x => x.Name));
        Assert.Equal("Mode", Assert.Single(message.Decode([255, 42, 0, 0, 0, 0, 0, 0])).Name);
    }

    [Theory]
    [InlineData("BA_DEF_ BO_ \"VFrameFormat\" INT 0 15;")]
    [InlineData("BA_ \"VFrameFormat\" 14;")]
    [InlineData("BA_ \"BusType\" BO_ 291 \"CAN FD\";")]
    public void Malformed_layout_attributes_do_not_silently_fall_back_to_classic_can(string metadata)
        => Assert.Throws<FormatException>(() => Parse("", tail: metadata));

    [Theory]
    [InlineData("BO_ 536870912 Bad: 8 ECU")]
    [InlineData("BO_ 2147483648 Bad: 65 ECU")]
    [InlineData("BO_ 291 A: 8 ECU\nBO_ 291 B: 8 ECU")]
    [InlineData("BO_ 291 A: 8 ECU\nBO_ 292 A: 8 ECU")]
    [InlineData("BO_ 291 A: 8 ECU\nCM_ SG_ 291 V \"unterminated")]
    public void Bad_files_fail_before_a_catalog_is_returned(string text) => Assert.Throws<FormatException>(() => DbcDatabase.Parse(text));

    [Fact]
    public void Cancellation_and_input_size_are_bounded()
    {
        using var stop = new CancellationTokenSource(); stop.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => DbcDatabase.Parse("BO_ 291 M: 0 ECU", stop.Token));
        Assert.Throws<ArgumentException>(() => DbcDatabase.Parse(new string(' ', DbcDatabase.MaximumTextCharacters + 1)));
    }

    [Fact]
    public void Bom_and_indentation_are_allowed_on_first_declaration()
        => Assert.Equal("First", Assert.Single(DbcDatabase.Parse("\uFEFF   BO_ 291 First: 0 ECU").Messages).Name);

    [Theory]
    [InlineData("18446744073709551615", "18446744073709551615")]
    [InlineData("-9223372036854775808", "-9223372036854775808")]
    [InlineData("  +1.25e2  ", "125")]
    [InlineData("1e-28", "0.0000000000000000000000000001")]
    [InlineData("1.2300000000000000000000000000000000", "1.23")]
    public void Shared_exact_number_parser_preserves_integer_and_decimal_text(string input, string expected)
        => Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), DbcNumber.ParseExact(input));

    [Theory]
    [InlineData("18446744073709551615.00000000001")]
    [InlineData("0.10000000000000000000000000001")]
    [InlineData("1e-29")]
    [InlineData("79228162514264337593543950336")]
    [InlineData("1,000")]
    [InlineData("NaN")]
    [InlineData("0xFF")]
    [InlineData("")]
    public void Shared_exact_number_parser_rejects_inexact_or_invalid_external_values(string input)
        => Assert.Throws<FormatException>(() => DbcNumber.ParseExact(input));

    [Fact]
    public void Shared_exact_number_parser_enforces_input_limit_and_dbc_parser_preserves_line_context()
    {
        Assert.Equal(1m, DbcNumber.ParseExact(new string('0', 127) + "1"));
        Assert.Throws<FormatException>(() => DbcNumber.ParseExact(new string('0', 128) + "1"));
        var error = Assert.Throws<FormatException>(() => Parse(" SG_ V : 0|8@1+ (1e-29,0) [0|0] \"\" ECU"));
        Assert.Contains("第 2 行", error.Message); Assert.IsType<FormatException>(error.InnerException);
    }
}
