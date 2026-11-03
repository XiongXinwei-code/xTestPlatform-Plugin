using System.Text;
using System.Text.Json;
using CAN.Dbc;
using CAN.Helpers;
using CAN.Models;
using MessagePack;
using Xunit;

namespace CAN.Tests;

public sealed class CanCrcDataIdTests
{
    // Independent published check vector: CRC-16/MODBUS("123456789") = 0x4B37.
    [Theory]
    [InlineData(CanCrcDataIdPlacement.BeforeData, "31 32 33", "456789")]
    [InlineData(CanCrcDataIdPlacement.AfterData, "37 38 39", "123456")]
    public void Data_id_joins_crc_stream_without_entering_the_wire_payload(CanCrcDataIdPlacement placement, string id, string data)
    {
        var setting = Modbus(id, placement);
        byte[] payload = Encoding.ASCII.GetBytes(data).Concat(new byte[2]).ToArray();
        var original = payload.ToArray();
        var actual = new CanPayloadProcessor(setting).Prepare(payload);
        Assert.Equal(new byte[] { 0x37, 0x4B }, actual[^2..]);
        Assert.Equal(original, payload);
        Assert.Equal(payload.Length, actual.Length);
        Assert.Equal(payload[..^2], actual[..^2]);
        Assert.True(DbcPayload.InspectProtection(actual, setting).CrcValid);
        actual[0] ^= 1;
        Assert.False(DbcPayload.InspectProtection(actual, setting).CrcValid);
    }

    [Fact]
    public void Disabled_and_legacy_settings_preserve_previous_results_and_serialization()
    {
        var legacy = JsonSerializer.Deserialize<CanPayloadProtectionSetting>("{\"CrcEnabled\":true,\"CrcStartByte\":6,\"CrcEndByte\":7,\"CrcAlgorithm\":\"Crc16Modbus\"}")!;
        Assert.False(legacy.CrcDataIdEnabled);
        Assert.Equal(CanCrcDataIdPlacement.BeforeData, legacy.CrcDataIdPlacement);
        var dormant = legacy.Copy(); dormant.CrcDataIdHex = "not valid"; dormant.CrcDataIdPlacement = (CanCrcDataIdPlacement)99;
        Assert.Equal(new CanPayloadProcessor(legacy).Prepare(new byte[8]), new CanPayloadProcessor(dormant).Prepare(new byte[8]));
        dormant.CrcDataIdEnabled = true; dormant.CrcEnabled = false;
        Assert.Equal(new byte[8], new CanPayloadProcessor(dormant).Prepare(new byte[8]));

        var enabled = Modbus("00 12", CanCrcDataIdPlacement.AfterData);
        var restored = MessagePackSerializer.Deserialize<CanPayloadProtectionSetting>(MessagePackSerializer.Serialize(enabled));
        Assert.True(restored.CrcDataIdEnabled); Assert.Equal("00 12", restored.CrcDataIdHex);
        Assert.Equal(CanCrcDataIdPlacement.AfterData, restored.CrcDataIdPlacement);
        Assert.True(new CanPayloadProcessor(enabled).Matches(restored));
        Assert.Equal("00 12", enabled.Copy().CrcDataIdHex);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1")]
    [InlineData("123")]
    [InlineData("GG")]
    [InlineData("0x")]
    [InlineData("1 2")]
    [InlineData("-12")]
    [InlineData("12-")]
    public void Invalid_enabled_data_id_is_rejected(string id)
    {
        var error = Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(Modbus(id), 8));
        Assert.Contains("Data ID", error.Message);
    }

    [Fact]
    public void Unknown_placement_and_oversized_input_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(Modbus("00", (CanCrcDataIdPlacement)99), 8));
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(Modbus(new string('A', 130)), 8));
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(Modbus(new string(' ', 10000)), 8));
    }

    [Theory]
    [InlineData("0x313233")]
    [InlineData("31-32-33")]
    [InlineData("31,32,33")]
    [InlineData("31\t32\n33")]
    public void Equivalent_byte_spellings_share_snapshot_identity(string spelling)
    {
        var baseline = Modbus("31 32 33");
        var processor = new CanPayloadProcessor(baseline);
        Assert.True(processor.Matches(Modbus(spelling)));
        Assert.Equal(new CanPayloadProcessor(baseline).Prepare(new byte[8]), new CanPayloadProcessor(Modbus(spelling)).Prepare(new byte[8]));
    }

    [Theory]
    [InlineData(CanCrcAlgorithm.Crc8, 1, 0xF4UL)]
    [InlineData(CanCrcAlgorithm.Crc8Autosar, 1, 0xDFUL)]
    [InlineData(CanCrcAlgorithm.Crc16Modbus, 2, 0x4B37UL)]
    [InlineData(CanCrcAlgorithm.Crc32IsoHdlc, 4, 0xCBF43926UL)]
    [InlineData(CanCrcAlgorithm.Crc64Xz, 8, 0x995DC9BBDF1939FAUL)]
    public void Presets_and_custom_models_keep_one_crc_stream_for_both_placements_and_output_orders(CanCrcAlgorithm algorithm, int width, ulong expected)
    {
        foreach (var placement in Enum.GetValues<CanCrcDataIdPlacement>())
        foreach (var order in Enum.GetValues<CanPayloadByteOrder>())
        foreach (bool custom in new[] { false, true })
        {
            var setting = Modbus(placement == CanCrcDataIdPlacement.BeforeData ? "313233" : "373839", placement);
            setting.CrcAlgorithm = algorithm; setting.CrcEndByte = 5 + width; setting.CrcByteOrder = order;
            if (custom)
            {
                var parameters = CanPayloadProtection.GetParameters(setting);
                setting.CrcAlgorithm = CanCrcAlgorithm.Custom; setting.CrcWidth = parameters.Width;
                setting.CrcPolynomial = parameters.Polynomial.ToString("X"); setting.CrcInitialValue = parameters.InitialValue.ToString("X");
                setting.CrcXorOutput = parameters.XorOutput.ToString("X");
                setting.CrcReflectInput = parameters.ReflectInput; setting.CrcReflectOutput = parameters.ReflectOutput;
            }
            var payload = Encoding.ASCII.GetBytes(placement == CanCrcDataIdPlacement.BeforeData ? "456789" : "123456").Concat(new byte[width]).ToArray();
            var output = new CanPayloadProcessor(setting).Prepare(payload);
            var expectedBytes = Enumerable.Range(0, width).Select(i => (byte)(expected >> (8 * (order == CanPayloadByteOrder.LittleEndian ? i : width - 1 - i))));
            Assert.Equal(expectedBytes, output[6..]);
            Assert.True(DbcPayload.InspectProtection(output, setting).CrcValid);
        }
    }

    [Theory]
    [InlineData(CanCrcFieldTreatment.Skip, CanCrcDataIdPlacement.BeforeData)]
    [InlineData(CanCrcFieldTreatment.Skip, CanCrcDataIdPlacement.AfterData)]
    [InlineData(CanCrcFieldTreatment.Zero, CanCrcDataIdPlacement.BeforeData)]
    [InlineData(CanCrcFieldTreatment.Zero, CanCrcDataIdPlacement.AfterData)]
    public void Full_FD_and_maximum_id_respect_range_crc_field_treatment_and_updated_rc(CanCrcFieldTreatment treatment, CanCrcDataIdPlacement placement)
    {
        byte[] id = Enumerable.Range(64, 64).Select(i => (byte)i).ToArray();
        var setting = Modbus(Convert.ToHexString(id), placement);
        setting.CrcStartByte = 20; setting.CrcEndByte = 21; setting.CrcDataStartByte = 2; setting.CrcFieldTreatment = treatment;
        setting.RcEnabled = true; setting.RcStartByte = 2; setting.RcEndByte = 2; setting.RcInitialValue = 17;
        var payload = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        var processor = new CanPayloadProcessor(setting);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var output = processor.Prepare(payload);
            var selected = payload[2..].ToList(); selected[0] = (byte)(17 + attempt);
            if (treatment == CanCrcFieldTreatment.Skip) selected.RemoveRange(18, 2);
            else { selected[18] = 0; selected[19] = 0; }
            byte[] stream = (placement == CanCrcDataIdPlacement.BeforeData ? id.Concat(selected) : selected.Concat(id)).ToArray();
            ushort crc = ReferenceModbus(stream);
            Assert.Equal(new byte[] { (byte)crc, (byte)(crc >> 8) }, output[20..22]);
            Assert.Equal((byte)(17 + attempt), output[2]); Assert.Equal(64, output.Length);
            Assert.Equal(payload[..2], output[..2]); Assert.True(DbcPayload.InspectProtection(output, setting).CrcValid);
            processor.CommitAttempt();
        }
        Assert.Equal(2, payload[2]);
    }

    [Fact]
    public void Data_id_can_supply_nonempty_crc_input_when_selected_payload_is_only_the_skipped_crc_field()
    {
        var setting = Modbus("313233343536373839"); setting.CrcDataStartByte = 6; setting.CrcDataEndByte = 7;
        Assert.Equal(new byte[] { 0x37, 0x4B }, new CanPayloadProcessor(setting).Prepare(new byte[8])[6..]);
        setting.CrcDataIdEnabled = false;
        Assert.Throws<ArgumentException>(() => CanPayloadProtection.Validate(setting, 8));
    }

    [Fact]
    public void Processor_freezes_data_id_and_snapshot_match_detects_real_changes()
    {
        var setting = Modbus("31 32 33"); var expected = setting.Copy(); var processor = new CanPayloadProcessor(setting);
        setting.CrcDataIdHex = "AABB"; setting.CrcDataIdPlacement = CanCrcDataIdPlacement.AfterData;
        Assert.False(processor.Matches(setting)); Assert.True(processor.Matches(expected));
        Assert.Equal(new byte[] { 0x37, 0x4B }, processor.Prepare(Encoding.ASCII.GetBytes("456789").Concat(new byte[2]).ToArray())[6..]);
    }

    internal static ushort ReferenceModbus(IEnumerable<byte> input)
    {
        ushort crc = 0xFFFF;
        foreach (byte value in input)
        {
            crc ^= value;
            for (int i = 0; i < 8; i++) crc = (ushort)((crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xA001);
        }
        return crc;
    }

    internal static CanPayloadProtectionSetting Modbus(string id, CanCrcDataIdPlacement placement = CanCrcDataIdPlacement.BeforeData) => new()
    {
        CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Crc16Modbus,
        CrcStartByte = 6, CrcEndByte = 7, CrcDataIdEnabled = true, CrcDataIdHex = id, CrcDataIdPlacement = placement
    };
}
