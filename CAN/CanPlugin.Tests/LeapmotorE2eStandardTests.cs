using System.Globalization;
using CAN.Dbc;
using CAN.Helpers;
using CAN.Models;
using CAN.UI.Dbc;
using Xunit;

namespace CAN.Tests;

public sealed class LeapmotorE2eStandardTests
{
    // QLP YSJ 0235-2024 §4.2.6, Table 3. This is the published example table,
    // not a default for any real ECU; production Data IDs come from its communication matrix.
    private static CanPayloadProtectionSetting ExampleProtection() => new()
    {
        RcEnabled = true, RcStartByte = 1, RcEndByte = 1,
        RcUseBitField = true, RcBitOffset = 0, RcBitLength = 4,
        RcInitialValue = 0, RcMinimum = 0, RcMaximum = 15,
        RcStep = 1, RcOverflow = CanCounterOverflow.Wrap,
        CrcEnabled = true, CrcStartByte = 0, CrcEndByte = 0,
        CrcDataStartByte = 1, CrcDataEndByte = -1,
        CrcFieldTreatment = CanCrcFieldTreatment.Skip,
        CrcDataIdEnabled = true, CrcDataIdMode = CanCrcDataIdMode.Profile2,
        CrcDataIdUseProfilePlacement = true,
        CrcCounterByte = 1, CrcCounterBitOffset = 0,
        CrcDataIdTable = Enumerable.Range(1, 16)
            .Select(value => value.ToString("X2", CultureInfo.InvariantCulture)).ToArray()
    };

    [Fact]
    public void Published_table_three_matches_all_sixteen_frames_and_seventeenth_wraps_to_zero()
    {
        byte[] expectedCrc = Convert.FromHexString("0E1B98310D189B65081D9E370B1E9DCD");
        var protection = ExampleProtection();
        var processor = new CanPayloadProcessor(protection);
        var source = new byte[8];

        for (int frame = 0; frame < 17; frame++)
        {
            int counter = frame % 16;
            byte[] expected = [expectedCrc[counter], (byte)counter, 0, 0, 0, 0, 0, 0];
            byte[] actual = processor.Prepare(source);
            Assert.Equal(expected, actual);
            Assert.Equal(actual, processor.Prepare(source)); // Preparation alone does not advance RC.
            Assert.True(DbcPayload.InspectProtection(actual, protection).ProtectionValid);
            processor.CommitAttempt();
        }

        Assert.Equal(new byte[8], source);
        Assert.Equal(0U, protection.RcInitialValue);
    }

    [Fact]
    public void Low_nibble_counter_preserves_A0_business_bits_and_data_id_is_not_transmitted()
    {
        var protection = ExampleProtection();
        var processor = new CanPayloadProcessor(protection);
        byte[] source = [0, 0xA0, 0, 0, 0, 0, 0, 0];

        for (int frame = 0; frame < 17; frame++)
        {
            int counter = frame % 16;
            byte[] actual = processor.Prepare(source);
            Assert.Equal(8, actual.Length);
            Assert.Equal((byte)(0xA0 | counter), actual[1]);
            Assert.Equal(source[2..], actual[2..]);
            Assert.True(DbcPayload.InspectProtection(actual, protection).ProtectionValid);

            var changedId = protection.Copy();
            changedId.RcInitialValue = (uint)counter;
            changedId.CrcDataIdTable[counter] = "FF";
            byte[] alternate = new CanPayloadProcessor(changedId).Prepare(source);
            Assert.NotEqual(actual[0], alternate[0]);
            Assert.Equal(actual[1..], alternate[1..]); // Only the CRC reflects the implicit Data ID.
            Assert.False(DbcPayload.InspectProtection(actual, changedId).CrcValid);
            processor.CommitAttempt();
        }

        Assert.Equal(0xA0, source[1]);
    }

    [Fact]
    public void Dbc_business_nibble_remains_editable_while_counter_and_crc_are_automatic()
    {
        var message = DbcDatabase.Parse("""
            BO_ 291 LeapmotorExample: 8 Node
             SG_ Checksum : 0|8@1+ (1,0) [0|255] "" Receiver
             SG_ Counter : 8|4@1+ (1,0) [0|15] "" Receiver
             SG_ Business : 12|4@1+ (1,0) [0|15] "" Receiver
             SG_ Voltage : 16|16@1+ (0.1,0) [0|6000] "V" Receiver
            """).Find(0x123, false);
        var protection = ExampleProtection();
        var automatic = DbcPayload.ValidateProtection(message, protection);
        Assert.Equal(new[] { "Checksum", "Counter" }, automatic.OrderBy(name => name));

        foreach (var signal in message.Signals)
        {
            var row = new DbcSignalRow(signal, new() { Name = signal.Name, Selected = true }, protection);
            bool isAutomatic = automatic.Contains(signal.Name);
            Assert.Equal(isAutomatic, row.IsAutomatic);
            Assert.Equal(!isAutomatic, row.CanSelect);
            Assert.Equal(!isAutomatic, row.IsEditable);
            Assert.Equal(isAutomatic, !string.IsNullOrEmpty(row.ProtectionNote));
        }

        byte[] source = message.Encode(new Dictionary<string, DbcSignalValue>
        {
            ["Checksum"] = new(0), ["Counter"] = new(0),
            ["Business"] = new(10), ["Voltage"] = new(12.5m)
        });
        var processor = new CanPayloadProcessor(protection);
        for (int counter = 0; counter < 16; counter++)
        {
            byte[] frame = processor.Prepare(source);
            var decoded = message.Decode(frame).ToDictionary(signal => signal.Name);
            Assert.Equal((decimal)counter, decoded["Counter"].RawValue);
            Assert.Equal(10m, decoded["Business"].PhysicalValue);
            Assert.Equal(12.5m, decoded["Voltage"].PhysicalValue);
            Assert.Equal((decimal)frame[0], decoded["Checksum"].RawValue);
            Assert.True(DbcPayload.InspectProtection(frame, protection).ProtectionValid);
            processor.CommitAttempt();
        }
        Assert.Equal(0xA0, source[1]);
    }

    [Fact]
    public void Dbc_signal_spanning_counter_and_business_nibbles_is_rejected_instead_of_partly_overwritten()
    {
        var message = DbcDatabase.Parse("""
            BO_ 291 MixedByte: 8 Node
             SG_ BusinessAndCounter : 8|8@1+ (1,0) [0|255] "" Receiver
            """).Find(0x123, false);

        var error = Assert.Throws<ArgumentException>(() => DbcPayload.ValidateProtection(message, ExampleProtection()));
        Assert.Contains("BusinessAndCounter", error.Message);
        Assert.Contains("不能部分覆盖", error.Message);
    }
}
