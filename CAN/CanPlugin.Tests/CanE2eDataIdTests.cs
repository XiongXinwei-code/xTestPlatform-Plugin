using System.Text.Json;
using CAN.Dbc;
using CAN.Helpers;
using CAN.Models;
using MessagePack;
using Xunit;

namespace CAN.Tests;

public sealed class CanE2eDataIdTests
{
    [Theory]
    [InlineData(CanCrcDataIdMode.Profile2, 0U, 0x0E)]
    [InlineData(CanCrcDataIdMode.Profile2, 1U, 0x1B)]
    [InlineData(CanCrcDataIdMode.Profile1Both, 0U, 0xCC)]
    [InlineData(CanCrcDataIdMode.Profile1Both, 1U, 0x91)]
    public void First_wire_counter_is_user_configured_and_used_before_increment(CanCrcDataIdMode mode, uint first, byte expectedCrc)
    {
        var setting = Profile(mode); setting.RcInitialValue = first;
        for (int restart = 0; restart < 2; restart++)
        {
            var processor = new CanPayloadProcessor(setting);
            var frame = processor.Prepare(new byte[8]);
            Assert.Equal(first, (uint)(frame[1] & 15)); Assert.Equal(expectedCrc, frame[0]);
            Assert.True(DbcPayload.InspectProtection(frame, setting).ProtectionValid);
            processor.CommitAttempt();
            Assert.Equal(first + 1, (uint)(processor.Prepare(new byte[8])[1] & 15));
        }
    }

    // AUTOSAR FO R24-11 §6.5.5 tables 6.4–6.13: independent published wire vectors.
    [Theory]
    [InlineData(CanCrcDataIdMode.Profile1Both, "0123", "CC00", "9101")]
    [InlineData(CanCrcDataIdMode.Profile1Alt, "0123", "CE00", "0201")]
    [InlineData(CanCrcDataIdMode.Profile1Low, "0023", "CE00", "9301")]
    [InlineData(CanCrcDataIdMode.Profile1Nibble, "0123", "2A10", "7711")]
    public void Profile1_matches_official_vectors(CanCrcDataIdMode mode, string id, string first, string second)
    {
        var setting = Profile(mode); setting.CrcProfile1DataIdHex = id;
        var processor = new CanPayloadProcessor(setting);
        Assert.Equal(Convert.FromHexString(first + "000000000000"), processor.Prepare(new byte[8]));
        processor.CommitAttempt();
        var output = processor.Prepare(new byte[8]);
        Assert.Equal(Convert.FromHexString(second + "000000000000"), output);
        Assert.True(DbcPayload.InspectProtection(output, setting).CrcValid);
    }

    // R24-11 §6.6.5 table 6.17, DataIDList 01..10. The plugin initial value is
    // the first wire counter (1 for standard P2), not the standard internal state (0).
    [Fact]
    public void Profile2_matches_all_sixteen_official_vectors_and_wraps()
    {
        var setting = Profile(CanCrcDataIdMode.Profile2);
        var processor = new CanPayloadProcessor(setting);
        byte[] crc = Convert.FromHexString("1B98310D189B65081D9E370B1E9DCD0E");
        for (int i = 0; i < 32; i++)
        {
            var actual = processor.Prepare(new byte[8]);
            Assert.Equal((i + 1) % 16, actual[1]); Assert.Equal(crc[i % 16], actual[0]);
            Assert.True(DbcPayload.InspectProtection(actual, setting).CrcValid);
            processor.CommitAttempt();
        }
    }

    [Theory]
    [InlineData(CanCrcDataIdMode.Profile1Both)]
    [InlineData(CanCrcDataIdMode.Profile1Alt)]
    [InlineData(CanCrcDataIdMode.Profile1Low)]
    [InlineData(CanCrcDataIdMode.Profile1Nibble)]
    public void Profile1_wraps_14_to_zero_and_uses_current_counter(CanCrcDataIdMode mode)
    {
        var setting = Profile(mode); setting.RcInitialValue = 14;
        var processor = new CanPayloadProcessor(setting);
        Assert.Equal(14, processor.Prepare(new byte[8])[1] & 15); processor.CommitAttempt();
        Assert.Equal(0, processor.Prepare(new byte[8])[1] & 15);
    }

    [Fact]
    public void Counter_preserves_neighbour_bits_and_receiver_selects_from_received_counter()
    {
        var setting = Profile(CanCrcDataIdMode.Profile2); setting.RcInitialValue = 7;
        var original = new byte[8]; original[1] = 0xA0;
        var output = new CanPayloadProcessor(setting).Prepare(original);
        Assert.Equal(0xA7, output[1]); Assert.Equal(0xA0, original[1]);
        setting.RcEnabled = false; setting.RcInitialValue = 12345;
        Assert.True(DbcPayload.InspectProtection(output, setting).CrcValid);
        setting.CrcDataIdTable[7] = "FF";
        Assert.False(DbcPayload.InspectProtection(output, setting).CrcValid);
    }

    [Fact]
    public void Table_is_frozen_copied_and_serialized_without_shared_arrays()
    {
        var setting = Profile(CanCrcDataIdMode.Profile2); var copy = setting.Copy();
        var processor = new CanPayloadProcessor(setting);
        foreach (var roundTrip in new[] {
            JsonSerializer.Deserialize<CanPayloadProtectionSetting>(JsonSerializer.Serialize(setting))!,
            MessagePackSerializer.Deserialize<CanPayloadProtectionSetting>(MessagePackSerializer.Serialize(setting)) })
            Assert.True(processor.Matches(roundTrip));
        setting.CrcDataIdTable[1] = "FF";
        Assert.Equal("02", copy.CrcDataIdTable[1]); Assert.True(processor.Matches(copy));
        Assert.False(processor.Matches(setting)); Assert.Equal(0x1B, processor.Prepare(new byte[8])[0]);
    }

    [Theory]
    [InlineData(0)] [InlineData(15)] [InlineData(17)]
    public void Table_must_have_exactly_sixteen_entries(int length)
    {
        var setting = Profile(CanCrcDataIdMode.Profile2); setting.CrcDataIdTable = Enumerable.Repeat("01", length).ToArray();
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(setting, 8));
    }

    [Theory]
    [InlineData("")] [InlineData("1 2")] [InlineData("100")] [InlineData("GG")] [InlineData("12 34")]
    public void Table_entries_must_be_single_hex_bytes(string text)
    {
        var setting = Profile(CanCrcDataIdMode.Profile2); setting.CrcDataIdTable[13] = text;
        Assert.Contains("13", Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(setting, 8)).Message);
    }

    [Theory]
    [InlineData(CanCrcDataIdMode.Profile1Low, "0100")]
    [InlineData(CanCrcDataIdMode.Profile1Nibble, "1000")]
    [InlineData(CanCrcDataIdMode.Profile1Both, "10000")]
    [InlineData(CanCrcDataIdMode.Profile1Both, "12 34")]
    public void Profile1_rejects_invalid_numeric_id(CanCrcDataIdMode mode, string id)
    {
        var setting = Profile(mode); setting.CrcProfile1DataIdHex = id;
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(setting, 8));
    }

    [Fact]
    public void Profiles_ignore_dormant_fixed_fields_but_legacy_mode_preserves_them()
    {
        var setting = Profile(CanCrcDataIdMode.Profile2);
        setting.CrcDataIdHex = "invalid"; setting.CrcDataIdPlacement = (CanCrcDataIdPlacement)99;
        setting.CrcAlgorithm = (CanCrcAlgorithm)99;
        Assert.Equal(0x1B, new CanPayloadProcessor(setting).Prepare(new byte[8])[0]);
        var legacy = JsonSerializer.Deserialize<CanPayloadProtectionSetting>("{}")!;
        Assert.Equal(CanCrcDataIdMode.Fixed, legacy.CrcDataIdMode); Assert.False(legacy.RcUseBitField);
    }

    internal static CanPayloadProtectionSetting Profile(CanCrcDataIdMode mode) => new()
    {
        RcEnabled = true, RcStartByte = 1, RcEndByte = 1, RcUseBitField = true, RcBitLength = 4,
        RcMaximum = mode == CanCrcDataIdMode.Profile2 ? 15U : 14U,
        RcInitialValue = mode == CanCrcDataIdMode.Profile2 ? 1U : 0U,
        CrcEnabled = true, CrcStartByte = 0, CrcEndByte = 0, CrcDataIdEnabled = true,
        CrcDataIdMode = mode, CrcProfile1DataIdHex = mode == CanCrcDataIdMode.Profile1Low ? "0023" : "0123",
        CrcDataIdTable = Enumerable.Range(1, 16).Select(i => i.ToString("X2")).ToArray()
    };

    [Fact]
    public void Receiver_retains_crc_correct_frames_with_invalid_counter_or_explicit_id()
    {
        var p1 = Profile(CanCrcDataIdMode.Profile1Both); p1.RcEnabled = false;
        byte[] invalidCounter = [0, 15, 0, 0, 0, 0, 0, 0];
        invalidCounter[0] = ReferenceP1(new byte[] { 0x23, 0x01 }.Concat(invalidCounter.Skip(1)));
        var counterCheck = DbcPayload.InspectProtection(invalidCounter, p1);
        Assert.True(counterCheck.CrcValid); Assert.False(counterCheck.CounterValid); Assert.False(counterCheck.ProtectionValid);
        Assert.Throws<ArgumentException>(() => new CanPayloadProcessor(p1).Prepare(invalidCounter));

        var nibble = Profile(CanCrcDataIdMode.Profile1Nibble);
        byte[] wrongId = [0, 0x20, 0, 0, 0, 0, 0, 0];
        wrongId[0] = ReferenceP1(new byte[] { 0x23, 0 }.Concat(wrongId.Skip(1)));
        var before = wrongId.ToArray(); var idCheck = DbcPayload.InspectProtection(wrongId, nibble);
        Assert.True(idCheck.CrcValid); Assert.True(idCheck.CounterValid);
        Assert.False(idCheck.DataIdValid); Assert.False(idCheck.ProtectionValid); Assert.Equal(before, wrongId);
    }

    [Fact]
    public void High_nibble_counter_and_low_nibble_id_preserve_adjacent_business_bits()
    {
        var setting = Profile(CanCrcDataIdMode.Profile1Nibble);
        setting.RcBitOffset = setting.CrcCounterBitOffset = 4; setting.RcInitialValue = 5;
        setting.CrcDataIdNibbleByte = 3; setting.CrcDataIdNibbleBitOffset = 0;
        byte[] payload = [0, 0x0F, 0x12, 0xA0, 0, 0, 0, 0];
        byte[] output = new CanPayloadProcessor(setting).Prepare(payload);
        Assert.Equal(0x5F, output[1]); Assert.Equal(0xA1, output[3]); Assert.Equal(0x12, output[2]);
        Assert.Equal(ReferenceP1(new byte[] { 0x23, 0 }.Concat(output.Skip(1))), output[0]);
        Assert.True(DbcPayload.InspectProtection(output, setting).ProtectionValid);
        Assert.True(CanPayloadProtection.ControlsBit(setting, 12)); Assert.False(CanPayloadProtection.ControlsBit(setting, 8));
        Assert.True(CanPayloadProtection.ControlsBit(setting, 24)); Assert.False(CanPayloadProtection.ControlsBit(setting, 28));
    }

    [Theory]
    [InlineData("counter-bounds")] [InlineData("counter-align")] [InlineData("counter-crc")]
    [InlineData("nibble-counter")] [InlineData("nibble-crc")] [InlineData("nibble-range")]
    [InlineData("rc-position")] [InlineData("rc-width")] [InlineData("rc-cross-byte")]
    [InlineData("rc-range")] [InlineData("rc-max")] [InlineData("crc-zero")]
    public void Invalid_profile_layouts_are_rejected(string scenario)
    {
        var setting = Profile(CanCrcDataIdMode.Profile1Nibble);
        switch (scenario)
        {
            case "counter-bounds": setting.CrcCounterByte = 8; break;
            case "counter-align": setting.CrcCounterBitOffset = 1; break;
            case "counter-crc": setting.CrcCounterByte = 0; break;
            case "nibble-counter": setting.CrcDataIdNibbleBitOffset = 0; break;
            case "nibble-crc": setting.CrcDataIdNibbleByte = 0; break;
            case "nibble-range": setting.CrcDataIdNibbleByte = 7; setting.CrcDataEndByte = 6; break;
            case "rc-position": setting.RcStartByte = setting.RcEndByte = 2; break;
            case "rc-width": setting.RcBitLength = 8; break;
            case "rc-cross-byte": setting.RcBitOffset = 7; break;
            case "rc-range": setting.RcEndByte = 2; break;
            case "rc-max": setting.RcMaximum = 15; break;
            case "crc-zero": setting.CrcFieldTreatment = CanCrcFieldTreatment.Zero; break;
        }
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(setting, 8));
    }

    [Fact]
    public void Null_and_empty_tables_fail_only_when_enabled_and_table_spellings_are_equivalent()
    {
        var setting = Profile(CanCrcDataIdMode.Profile2);
        var processor = new CanPayloadProcessor(setting);
        setting.CrcDataIdTable = Enumerable.Range(1, 16).Select(i => $" 0x{i:x} ").ToArray();
        Assert.True(processor.Matches(setting));
        setting.CrcDataIdTable = null!;
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(setting, 8));
        setting.CrcDataIdEnabled = false; setting.CrcAlgorithm = CanCrcAlgorithm.Crc8;
        _ = new CanPayloadProcessor(setting.Copy()).Prepare(new byte[8]);
    }

    [Fact]
    public void Fd_full_length_and_custom_selected_range_include_table_byte_without_expanding_payload()
    {
        var setting = Profile(CanCrcDataIdMode.Profile2);
        setting.CrcStartByte = setting.CrcEndByte = 63;
        setting.CrcDataStartByte = 1;
        byte[] payload = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        byte[] output = new CanPayloadProcessor(setting).Prepare(payload);
        Assert.Equal(64, output.Length); Assert.Equal(0, output[0]); Assert.Equal(payload[2..63], output[2..63]);
        Assert.True(DbcPayload.InspectProtection(output, setting).ProtectionValid);
        output[0] = 255; Assert.True(DbcPayload.InspectProtection(output, setting).CrcValid);
        output[62] ^= 1; Assert.False(DbcPayload.InspectProtection(output, setting).CrcValid);
    }

    private static byte ReferenceP1(IEnumerable<byte> input)
    {
        int value = 0;
        foreach (byte item in input)
        {
            value ^= item;
            for (int i = 0; i < 8; i++) value = ((value & 128) != 0 ? (value << 1) ^ 0x1D : value << 1) & 255;
        }
        return (byte)value;
    }

    [Theory]
    [InlineData(CanCrcDataIdMode.Profile2, CanCrcDataIdPlacement.BeforeData)]
    [InlineData(CanCrcDataIdMode.Profile2, CanCrcDataIdPlacement.AfterData)]
    [InlineData(CanCrcDataIdMode.Profile1Both, CanCrcDataIdPlacement.BeforeData)]
    [InlineData(CanCrcDataIdMode.Profile1Both, CanCrcDataIdPlacement.AfterData)]
    [InlineData(CanCrcDataIdMode.Profile1Alt, CanCrcDataIdPlacement.BeforeData)]
    [InlineData(CanCrcDataIdMode.Profile1Alt, CanCrcDataIdPlacement.AfterData)]
    [InlineData(CanCrcDataIdMode.Profile1Low, CanCrcDataIdPlacement.BeforeData)]
    [InlineData(CanCrcDataIdMode.Profile1Low, CanCrcDataIdPlacement.AfterData)]
    [InlineData(CanCrcDataIdMode.Profile1Nibble, CanCrcDataIdPlacement.BeforeData)]
    [InlineData(CanCrcDataIdMode.Profile1Nibble, CanCrcDataIdPlacement.AfterData)]
    public void Explicit_custom_placement_keeps_counter_selection_but_moves_implicit_id(CanCrcDataIdMode mode, CanCrcDataIdPlacement placement)
    {
        var setting = Profile(mode); setting.CrcDataIdUseProfilePlacement = false; setting.CrcDataIdPlacement = placement;
        setting.RcInitialValue = 3;
        var processor = new CanPayloadProcessor(setting);
        byte[] output = processor.Prepare(new byte[] { 0, 0xA0, 0x78, 0x56, 0x34, 0x12, 0, 0 });
        byte[] selectedId = mode switch
        {
            CanCrcDataIdMode.Profile2 => [4],
            CanCrcDataIdMode.Profile1Both => [0x23, 1],
            CanCrcDataIdMode.Profile1Alt => [1],
            CanCrcDataIdMode.Profile1Low => [0x23],
            _ => [0x23, 0]
        };
        var stream = placement == CanCrcDataIdPlacement.BeforeData ? selectedId.Concat(output.Skip(1)) : output.Skip(1).Concat(selectedId);
        byte expected = mode == CanCrcDataIdMode.Profile2 ? ReferenceP2(stream) : ReferenceP1(stream);
        Assert.Equal(expected, output[0]); Assert.True(DbcPayload.InspectProtection(output, setting).ProtectionValid);
        Assert.Contains("自定义", CanPayloadProtection.GetParameters(setting).Name);
        var restored = MessagePackSerializer.Deserialize<CanPayloadProtectionSetting>(MessagePackSerializer.Serialize(setting));
        Assert.False(restored.CrcDataIdUseProfilePlacement); Assert.True(processor.Matches(restored)); Assert.True(processor.Matches(setting.Copy()));
    }

    [Fact]
    public void Standard_order_is_default_and_ignores_dormant_custom_order_but_custom_order_is_validated()
    {
        var setting = Profile(CanCrcDataIdMode.Profile2);
        Assert.True(setting.CrcDataIdUseProfilePlacement);
        setting.CrcDataIdPlacement = (CanCrcDataIdPlacement)99;
        Assert.Equal(0x1B, new CanPayloadProcessor(setting).Prepare(new byte[8])[0]);
        setting.CrcDataIdUseProfilePlacement = false;
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(setting, 8));
        var legacy = JsonSerializer.Deserialize<CanPayloadProtectionSetting>("{\"CrcDataIdMode\":\"Profile2\"}")!;
        Assert.True(legacy.CrcDataIdUseProfilePlacement);
        var oldMap = MessagePackSerializer.ConvertFromJson("{\"CrcDataIdMode\":1}");
        Assert.True(MessagePackSerializer.Deserialize<CanPayloadProtectionSetting>(oldMap).CrcDataIdUseProfilePlacement);
    }

    private static byte ReferenceP2(IEnumerable<byte> input)
    {
        int value = 255;
        foreach (byte item in input)
        {
            value ^= item;
            for (int i = 0; i < 8; i++) value = ((value & 128) != 0 ? (value << 1) ^ 0x2F : value << 1) & 255;
        }
        return (byte)(value ^ 255);
    }
}
