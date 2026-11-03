using System.Text;
using System.Text.Json;
using CAN.Helpers;
using CAN.Models;
using Xunit;

namespace CAN.Tests;

public sealed class CanPayloadProtectionTests
{
    [Theory]
    [InlineData(CanCrcAlgorithm.Crc8, 1, 0xF4UL)]
    [InlineData(CanCrcAlgorithm.Crc8SaeJ1850, 1, 0x4BUL)]
    [InlineData(CanCrcAlgorithm.Crc8Autosar, 1, 0xDFUL)]
    [InlineData(CanCrcAlgorithm.Crc16Modbus, 2, 0x4B37UL)]
    [InlineData(CanCrcAlgorithm.Crc16CcittFalse, 2, 0x29B1UL)]
    [InlineData(CanCrcAlgorithm.Crc32IsoHdlc, 4, 0xCBF43926UL)]
    [InlineData(CanCrcAlgorithm.Crc8MaximDow, 1, 0xA1UL)]
    [InlineData(CanCrcAlgorithm.Crc8Rohc, 1, 0xD0UL)]
    [InlineData(CanCrcAlgorithm.Crc16Arc, 2, 0xBB3DUL)]
    [InlineData(CanCrcAlgorithm.Crc16X25, 2, 0x906EUL)]
    [InlineData(CanCrcAlgorithm.Crc16Xmodem, 2, 0x31C3UL)]
    [InlineData(CanCrcAlgorithm.Crc16Kermit, 2, 0x2189UL)]
    [InlineData(CanCrcAlgorithm.Crc16Usb, 2, 0xB4C8UL)]
    [InlineData(CanCrcAlgorithm.Crc16Dnp, 2, 0xEA82UL)]
    [InlineData(CanCrcAlgorithm.Crc32C, 4, 0xE3069283UL)]
    [InlineData(CanCrcAlgorithm.Crc32Mpeg2, 4, 0x0376E6E7UL)]
    [InlineData(CanCrcAlgorithm.Crc32Bzip2, 4, 0xFC891918UL)]
    [InlineData(CanCrcAlgorithm.Crc32Jamcrc, 4, 0x340BC6D9UL)]
    [InlineData(CanCrcAlgorithm.Crc64Ecma182, 8, 0x6C40DF5F0B497347UL)]
    [InlineData(CanCrcAlgorithm.Crc64Xz, 8, 0x995DC9BBDF1939FAUL)]
    public void Standard_crc_check_vectors_match_catalogue(CanCrcAlgorithm algorithm, int bytes, ulong expected)
    {
        var setting = new CanPayloadProtectionSetting
        {
            CrcEnabled = true, CrcAlgorithm = algorithm, CrcStartByte = 9, CrcEndByte = 8 + bytes,
            CrcDataStartByte = 0, CrcDataEndByte = 8, CrcByteOrder = CanPayloadByteOrder.BigEndian
        };
        byte[] data = [.. Encoding.ASCII.GetBytes("123456789"), .. new byte[bytes]];
        var result = new CanPayloadProcessor(setting).Prepare(data);
        ulong actual = 0;
        foreach (var value in result.Skip(9)) actual = (actual << 8) | value;
        Assert.Equal(expected, actual);
        Assert.All(data.Skip(9), b => Assert.Equal(0, b));

        var parameters = CanPayloadProtection.GetParameters(setting);
        var custom = setting.Copy(); custom.CrcAlgorithm = CanCrcAlgorithm.Custom;
        custom.CrcWidth = parameters.Width; custom.CrcPolynomial = parameters.Polynomial.ToString("x");
        custom.CrcInitialValue = parameters.InitialValue.ToString("x"); custom.CrcXorOutput = parameters.XorOutput.ToString("x");
        custom.CrcReflectInput = parameters.ReflectInput; custom.CrcReflectOutput = parameters.ReflectOutput;
        Assert.Equal(result, new CanPayloadProcessor(custom).Prepare(data));
    }

    [Fact]
    public void Counter_reserves_once_per_attempt_and_wraps_inclusively()
    {
        var processor = new CanPayloadProcessor(new() { RcEnabled = true, RcMinimum = 2, RcMaximum = 5, RcInitialValue = 4, RcStep = 3 });
        Assert.Equal(4, processor.Prepare([0])[0]);
        Assert.Equal(4, processor.Prepare([0])[0]);
        processor.CommitAttempt();
        Assert.Equal(3, processor.Prepare([0])[0]);
    }

    [Fact]
    public void Crc_is_calculated_after_counter_and_preserves_other_bytes()
    {
        var setting = new CanPayloadProtectionSetting
        {
            RcEnabled = true, RcStartByte = 1, RcEndByte = 1, RcInitialValue = 0x31,
            CrcEnabled = true, CrcStartByte = 10, CrcEndByte = 10, CrcDataStartByte = 1, CrcDataEndByte = 9
        };
        byte[] input = [0xAA, 0, .. Encoding.ASCII.GetBytes("23456789"), 0xBB, 0xCC];
        var result = new CanPayloadProcessor(setting).Prepare(input);
        Assert.Equal(0x31, result[1]); Assert.Equal(0xF4, result[10]);
        Assert.Equal(0xAA, result[0]); Assert.Equal(0xCC, result[11]);
        Assert.Equal(0, input[1]); Assert.Equal(0xBB, input[10]);
    }

    [Theory]
    [InlineData(CanPayloadByteOrder.LittleEndian, 0x37, 0x4B)]
    [InlineData(CanPayloadByteOrder.BigEndian, 0x4B, 0x37)]
    public void Crc_in_middle_is_skipped_and_written_in_selected_byte_order(CanPayloadByteOrder order, byte first, byte second)
    {
        var setting = new CanPayloadProtectionSetting
        {
            CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Crc16Modbus,
            CrcStartByte = 4, CrcEndByte = 5, CrcByteOrder = order
        };
        byte[] input = [.. Encoding.ASCII.GetBytes("1234"), 0xAA, 0xBB, .. Encoding.ASCII.GetBytes("56789")];
        var result = new CanPayloadProcessor(setting).Prepare(input);
        Assert.Equal(first, result[4]); Assert.Equal(second, result[5]);
        Assert.Equal(input.Take(4), result.Take(4)); Assert.Equal(input.Skip(6), result.Skip(6));
    }

    [Fact]
    public void Zero_treatment_uses_zero_instead_of_original_crc_field()
    {
        var zero = new CanPayloadProtectionSetting { CrcEnabled = true, CrcStartByte = 1, CrcEndByte = 1, CrcFieldTreatment = CanCrcFieldTreatment.Zero };
        var reference = new CanPayloadProtectionSetting { CrcEnabled = true, CrcStartByte = 3, CrcEndByte = 3, CrcDataEndByte = 2 };
        byte[] a = new CanPayloadProcessor(zero).Prepare([1, 0xFF, 3]);
        byte[] b = new CanPayloadProcessor(reference).Prepare([1, 0, 3, 0]);
        Assert.Equal(b[3], a[1]);
        var skipped = zero.Copy(); skipped.CrcFieldTreatment = CanCrcFieldTreatment.Skip;
        Assert.NotEqual(new CanPayloadProcessor(skipped).Prepare([1, 0xFF, 3])[1], a[1]);
    }

    [Theory]
    [InlineData(CanPayloadByteOrder.LittleEndian, 0x34, 0x12)]
    [InlineData(CanPayloadByteOrder.BigEndian, 0x12, 0x34)]
    public void Counter_byte_order_and_inclusive_destination(CanPayloadByteOrder order, byte first, byte second)
    {
        var processor = new CanPayloadProcessor(new()
        {
            RcEnabled = true, RcStartByte = 1, RcEndByte = 2, RcMaximum = ushort.MaxValue,
            RcInitialValue = 0x1234, RcByteOrder = order
        });
        Assert.Equal(new byte[] { 0xAA, first, second, 0xBB }, processor.Prepare([0xAA, 0, 0, 0xBB]));
    }

    [Fact]
    public void Negative_step_wraps_and_maximum_width_avoids_overflow()
    {
        var processor = new CanPayloadProcessor(new() { RcEnabled = true, RcMinimum = 2, RcMaximum = 5, RcInitialValue = 2, RcStep = -3 });
        Assert.Equal(2, processor.Prepare([0])[0]); processor.CommitAttempt();
        Assert.Equal(3, processor.Prepare([0])[0]);
        var wide = new CanPayloadProcessor(new() { RcEnabled = true, RcEndByte = 3, RcMaximum = uint.MaxValue, RcInitialValue = uint.MaxValue, RcStep = uint.MaxValue });
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, wide.Prepare(new byte[4])); wide.CommitAttempt();
        Assert.Equal(new byte[] { 254, 255, 255, 255 }, wide.Prepare(new byte[4]));
    }

    [Theory]
    [InlineData(5, 1, 5)]
    [InlineData(2, -1, 2)]
    public void Hold_saturates_at_limit(uint initial, long step, byte expected)
    {
        var processor = new CanPayloadProcessor(new() { RcEnabled = true, RcMinimum = 2, RcMaximum = 5, RcInitialValue = initial, RcStep = step, RcOverflow = CanCounterOverflow.Hold });
        processor.Prepare([0]); processor.CommitAttempt(); Assert.Equal(expected, processor.Prepare([0])[0]);
    }

    [Fact]
    public void Error_rule_allows_boundary_attempt_then_rejects_the_next()
    {
        var processor = new CanPayloadProcessor(new() { RcEnabled = true, RcInitialValue = 255, RcOverflow = CanCounterOverflow.Error });
        Assert.Equal(255, processor.Prepare([0])[0]); processor.CommitAttempt();
        Assert.Throws<InvalidOperationException>(() => processor.Prepare([0]));
    }

    [Fact]
    public void Zero_step_and_single_value_ranges_are_allowed()
    {
        var processor = new CanPayloadProcessor(new() { RcEnabled = true, RcInitialValue = 7, RcMinimum = 7, RcMaximum = 7, RcStep = -100 });
        for (int i = 0; i < 3; i++) { Assert.Equal(7, processor.Prepare([0])[0]); processor.CommitAttempt(); }
        var held = new CanPayloadProcessor(new() { RcEnabled = true, RcInitialValue = 7, RcStep = 0 });
        held.Prepare([0]); held.CommitAttempt(); Assert.Equal(7, held.Prepare([0])[0]);
    }

    [Fact]
    public void Configuration_is_frozen_and_copy_does_not_copy_event_handlers()
    {
        var setting = new CanPayloadProtectionSetting { RcEnabled = true, RcInitialValue = 3 };
        int notifications = 0; setting.PropertyChanged += (_, _) => notifications++;
        var copy = setting.Copy(); var processor = new CanPayloadProcessor(setting);
        Assert.True(processor.Matches(copy)); copy.RcInitialValue = 9;
        Assert.Equal(0, notifications); Assert.False(processor.Matches(copy));
        setting.RcInitialValue = 7; Assert.Equal(1, notifications);
        Assert.Equal(3, processor.Prepare([0])[0]);
    }

    [Fact]
    public void Legacy_disabled_configuration_preserves_payload_and_empty_frames()
    {
        var processor = new CanPayloadProcessor(null); byte[] data = [1, 2, 3];
        Assert.Equal(data, processor.Prepare(data)); Assert.NotSame(data, processor.Prepare(data));
        Assert.Empty(processor.Prepare([]));
        var disabled = new CanPayloadProtectionSetting { CrcWidth = -1, CrcPolynomial = "not hex", RcStartByte = -1 };
        Assert.Equal(data, new CanPayloadProcessor(disabled).Prepare(data));
    }

    [Fact]
    public void Fd_last_byte_is_valid_and_outside_actual_payload_is_rejected()
    {
        var setting = new CanPayloadProtectionSetting { RcEnabled = true, RcStartByte = 63, RcEndByte = 63, RcInitialValue = 17 };
        var processor = new CanPayloadProcessor(setting);
        Assert.Equal(17, processor.Prepare(new byte[64])[63]);
        Assert.Throws<ArgumentException>(() => processor.Prepare(new byte[63]));
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.ValidateConfiguration(setting, 8));
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(setting, 65));
    }

    [Fact]
    public void Failed_prepare_cannot_consume_counter_and_commit_requires_prepare()
    {
        var processor = new CanPayloadProcessor(new() { RcEnabled = true, RcInitialValue = 3 });
        Assert.Throws<InvalidOperationException>(() => processor.CommitAttempt());
        Assert.Throws<ArgumentException>(() => processor.Prepare([]));
        Assert.Throws<InvalidOperationException>(() => processor.CommitAttempt());
        Assert.Equal(3, processor.Prepare([0])[0]); processor.CommitAttempt();
        Assert.Throws<InvalidOperationException>(() => processor.CommitAttempt());
        Assert.Equal(4, processor.Prepare([0])[0]);
    }

    [Fact]
    public void Custom_hex_is_not_decimal_and_reflection_is_independently_configurable()
    {
        var setting = new CanPayloadProtectionSetting
        {
            CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Custom, CrcWidth = 16,
            CrcPolynomial = "8005", CrcInitialValue = "FFFF", CrcXorOutput = "0000",
            CrcReflectInput = true, CrcReflectOutput = false, CrcStartByte = 9, CrcEndByte = 10,
            CrcByteOrder = CanPayloadByteOrder.BigEndian
        };
        // MODBUS unreflected final register = bit-reversal of 0x4B37 = 0xECD2.
        var result = new CanPayloadProcessor(setting).Prepare([.. Encoding.ASCII.GetBytes("123456789"), 0, 0]);
        Assert.Equal(new byte[] { 0xEC, 0xD2 }, result.Skip(9));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    [InlineData(12)] [InlineData(13)] [InlineData(14)] [InlineData(15)] [InlineData(16)] [InlineData(17)]
    public void Invalid_configuration_is_rejected_before_mutation(int scenario)
    {
        var s = new CanPayloadProtectionSetting { RcEnabled = true, CrcEnabled = true };
        switch (scenario)
        {
            case 0: s.RcStartByte = -1; break;
            case 1: s.RcEndByte = 4; break;
            case 2: s.RcMaximum = 256; break;
            case 3: s.RcMinimum = 10; s.RcMaximum = 9; break;
            case 4: s.RcInitialValue = 256; break;
            case 5: s.RcStep = long.MaxValue; break;
            case 6: s.RcOverflow = (CanCounterOverflow)999; break;
            case 7: s.RcByteOrder = (CanPayloadByteOrder)999; break;
            case 8: s.CrcAlgorithm = (CanCrcAlgorithm)999; break;
            case 9: s.CrcStartByte = 0; s.CrcEndByte = 0; break;
            case 10: s.CrcAlgorithm = CanCrcAlgorithm.Crc16Arc; break;
            case 11: s.CrcByteOrder = (CanPayloadByteOrder)999; break;
            case 12: s.CrcFieldTreatment = (CanCrcFieldTreatment)999; break;
            case 13: s.CrcDataStartByte = 2; s.CrcDataEndByte = 1; break;
            case 14: s.CrcDataStartByte = 7; s.CrcDataEndByte = 7; break;
            case 15: s.CrcAlgorithm = CanCrcAlgorithm.Custom; s.CrcWidth = 7; break;
            case 16: s.CrcAlgorithm = CanCrcAlgorithm.Custom; s.CrcPolynomial = "100"; break;
            case 17: s.CrcAlgorithm = CanCrcAlgorithm.Custom; s.CrcPolynomial = "06"; break;
        }
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(s, 8));
    }

    [Theory]
    [InlineData("")] [InlineData("-1")] [InlineData("xyz")] [InlineData("0x100")] [InlineData("10000000000000000")]
    public void Invalid_custom_hex_is_rejected(string value)
    {
        var s = new CanPayloadProtectionSetting { CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Custom, CrcInitialValue = value };
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(s, 8));
    }

    [Fact]
    public void Enum_names_serialize_for_platform_json_configuration()
    {
        var setting = new CanPayloadProtectionSetting { CrcAlgorithm = CanCrcAlgorithm.Crc64Xz, RcOverflow = CanCounterOverflow.Hold };
        string json = JsonSerializer.Serialize(setting);
        Assert.Contains("\"Crc64Xz\"", json); Assert.Contains("\"Hold\"", json);
        Assert.Equal(CanCrcAlgorithm.Crc64Xz, JsonSerializer.Deserialize<CanPayloadProtectionSetting>(json)!.CrcAlgorithm);
    }
}
