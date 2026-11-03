using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CAN.Dbc;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanE2eDataIdIntegrationTests
{
    // Deliberately synthetic tables: no device-specific Data ID is inferred by these tests.
    private static string[] Table(int start) => Enumerable.Range(start, 16).Select(i => i.ToString("X2", CultureInfo.InvariantCulture)).ToArray();

    private static CanPayloadProtectionSetting Protection(int tableStart = 1, uint initial = 14,
        CanCrcDataIdMode mode = CanCrcDataIdMode.Profile2) => new()
    {
        RcEnabled = true, RcStartByte = 1, RcEndByte = 1, RcUseBitField = true,
        RcBitOffset = 0, RcBitLength = 4, RcInitialValue = initial,
        RcMinimum = 0, RcMaximum = mode == CanCrcDataIdMode.Profile2 ? 15u : 14u,
        RcStep = 1, RcOverflow = CanCounterOverflow.Wrap,
        CrcEnabled = true, CrcStartByte = 0, CrcEndByte = 0,
        CrcDataStartByte = 0, CrcDataEndByte = -1, CrcFieldTreatment = CanCrcFieldTreatment.Skip,
        CrcDataIdEnabled = true, CrcDataIdMode = mode, CrcDataIdTable = Table(tableStart),
        CrcProfile1DataIdHex = "0123", CrcCounterByte = 1, CrcCounterBitOffset = 0,
        CrcDataIdNibbleByte = 1, CrcDataIdNibbleBitOffset = 4,
        // Dedicated Data ID modes determine the effective CRC; a dormant old selection must not leak in.
        CrcAlgorithm = CanCrcAlgorithm.Crc16Modbus
    };

    private static (IExecutionContext Context, DbcTestContext State) Context(IStepPlugin plugin, object setting, CanConnectionResource owner)
    {
        var context = DispatchProxy.Create<IExecutionContext, DbcTestContext>();
        var state = (DbcTestContext)(object)context;
        state.Plugin = plugin; state.Setting = setting; state.Adapter = owner;
        state.Variables["Locals.Result"] = "unchanged";
        return (context, state);
    }

    private static void Completed(ExecutionResult result) =>
        Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());

    private static async Task Until(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(5000);
        while (!condition()) await Task.Delay(5, deadline.Token);
    }

    private static CanReceiveRouter Router(CanConnectionResource owner) => (CanReceiveRouter)typeof(CanConnectionResource)
        .GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    [Fact]
    public async Task Multi_write_uses_each_frames_counter_and_own_table_across_full_wrap()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        var setting = new CanWriteSetting { UseMessageList = true, Messages =
        [
            new() { CanId = "\"0x123\"", Data = "\"00 A0 11 22 33 44 55 66\"", Protection = Protection(1, 14) },
            new() { CanId = "\"0x124\"", Data = "\"00 B0 11 22 33 44 55 66\"", UseFdFrame = true, Protection = Protection(0xA0, 5) }
        ] };
        var (context, state) = Context(new CanWritePlugin(), setting, owner);
        try
        {
            for (int call = 0; call < 20; call++) Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            var sent = raw.Sent.ToArray(); Assert.Equal(40, sent.Length);
            for (int call = 0; call < 20; call++)
            {
                var first = sent[2 * call]; var second = sent[2 * call + 1];
                Assert.Equal(0x123u, first.Id); Assert.Equal(0x124u, second.Id);
                Assert.Equal((byte)(0xA0 | ((14 + call) & 15)), first.Data[1]);
                Assert.Equal((byte)(0xB0 | ((5 + call) & 15)), second.Data[1]);
                Assert.False(first.IsFd); Assert.True(second.IsFd);
                AssertP2(first.Data, Table(1)); AssertP2(second.Data, Table(0xA0));
            }
            Assert.Equal("\"00 A0 11 22 33 44 55 66\"", setting.Messages[0].Data);
            Assert.Equal("\"00 B0 11 22 33 44 55 66\"", setting.Messages[1].Data);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Invalid_second_table_rejects_entire_multi_write_without_advancing_first_counter()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var setting = new CanWriteSetting { UseMessageList = true, Messages =
        [
            new() { CanId = "\"0x123\"", Data = "\"00 A0 00 00 00 00 00 00\"", Protection = Protection(1, 7) },
            new() { CanId = "\"0x124\"", Data = "\"00 B0 00 00 00 00 00 00\"", Protection = Protection(0xA0, 12) }
        ] };
        setting.Messages[1].Protection.CrcDataIdTable = Table(0xA0)[..15];
        var (context, state) = Context(new CanWritePlugin(), setting, owner);
        try
        {
            var failed = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, failed.StepResult.Status); Assert.Empty(raw.Sent);
            Assert.Contains("16", failed.StepResult.Error!.Message);
            setting.Messages[1].Protection.CrcDataIdTable = Table(0xA0);
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            Assert.Equal(new byte[] { 0xA7, 0xBC }, raw.Sent.Select(frame => frame.Data[1]));
            AssertP2(raw.Sent.First().Data, Table(1)); AssertP2(raw.Sent.Last().Data, Table(0xA0));
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(14u)]
    [InlineData(0u)]
    [InlineData(1u)]
    public async Task Cyclic_table_is_deep_frozen_and_restart_adopts_new_table_with_initial_counter(uint initial)
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var setting = new CanCyclicSendStartSetting { Messages =
        [new() { CanId = "\"0x123\"", Data = "\"00 C0 00 00 00 00 00 00\"", CycleTimeMs = 5, Protection = Protection(1, initial) }] };
        var (context, state) = Context(new CanCyclicSendStartPlugin(), setting, owner);
        try
        {
            for (int restart = 0; restart < 2; restart++)
            {
                var expectedTable = Table(restart == 0 ? 1 : 0x80);
                setting.Messages[0].Protection.CrcDataIdTable = expectedTable.ToArray();
                int offset = raw.Sent.Count;
                Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
                await Until(() => raw.Sent.Count >= offset + 3);
                int beforeMutation = raw.Sent.Count;
                Array.Fill(setting.Messages[0].Protection.CrcDataIdTable, "invalid");
                await Until(() => raw.Sent.Count >= beforeMutation + 3);
                await owner.StopJobAsync("CyclicTask1", 1000, default);
                var sent = raw.Sent.Skip(offset).ToArray();
                Assert.Equal(Enumerable.Range((int)initial, 3).Select(value => (byte)(0xC0 | (value & 15))),
                    sent.Take(3).Select(frame => frame.Data[1]));
                Assert.All(sent, frame => AssertP2(frame.Data, expectedTable));
                int stopped = raw.Sent.Count; await Task.Delay(25); Assert.Equal(stopped, raw.Sent.Count);
            }
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Invalid_supplied_counter_in_second_frame_rejects_batch_before_first_submission()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var second = Protection(mode: CanCrcDataIdMode.Profile1Both); second.RcEnabled = false;
        var setting = new CanWriteSetting { UseMessageList = true, Messages =
        [
            new() { CanId = "\"0x123\"", Data = "\"00 A0 00 00 00 00 00 00\"", Protection = Protection(1, 7) },
            new() { CanId = "\"0x124\"", Data = "\"00 0F 00 00 00 00 00 00\"", Protection = second }
        ] };
        var (context, state) = Context(new CanWritePlugin(), setting, owner);
        try
        {
            var failed = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, failed.StepResult.Status); Assert.Empty(raw.Sent);
            Assert.Contains("Counter", failed.StepResult.Error!.Message);
            setting.Messages[1].Data = "\"00 00 00 00 00 00 00 00\"";
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            Assert.Equal(0xA7, raw.Sent.First().Data[1]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Raw_write_without_RC_generation_selects_Profile1_alternate_ID_from_supplied_counter()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var protection = Protection(mode: CanCrcDataIdMode.Profile1Alt); protection.RcEnabled = false;
        var setting = new CanWriteSetting { CanId = "\"0x123\"", Protection = protection };
        var (context, state) = Context(new CanWritePlugin(), setting, owner);
        try
        {
            foreach (byte counter in new byte[] { 4, 1, 14, 3 })
            {
                setting.Data = $"\"00 {counter:X2} 00 00 00 00 00 00\"";
                Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
                var sent = raw.Sent.Last();
                Assert.Equal(counter, sent.Data[1]);
                Assert.Equal(ReferenceCrc(new[] { (byte)((counter & 1) == 0 ? 0x23 : 0x01) }.Concat(sent.Data.Skip(1)), 0x1D, 0, 0), sent.Data[0]);
            }
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false, CanCrcDataIdMode.Profile2)]
    [InlineData(true, CanCrcDataIdMode.Profile2)]
    [InlineData(false, CanCrcDataIdMode.Profile1Nibble)]
    [InlineData(true, CanCrcDataIdMode.Profile1Nibble)]
    public async Task Dbc_write_read_and_capture_preserve_business_nibble_and_verify_received_counter(bool capture, CanCrcDataIdMode mode)
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var write = DbcWrite(mode); var (writeContext, writeState) = Context(new DbcWritePlugin(), write, owner);
        try
        {
            Completed(await writeState.Plugin.CreateExecutor().ExecuteAsync(writeContext));
            var frame = Assert.Single(raw.Sent); var original = frame.Data.ToArray();
            Assert.Equal((byte)(mode == CanCrcDataIdMode.Profile2 ? 0xDE : 0x1E), frame.Data[1]);
            Assert.Equal(new byte[] { 125, 0 }, frame.Data[2..4]);
            if (mode == CanCrcDataIdMode.Profile2) AssertP2(frame.Data, Table(1));
            else Assert.Equal(ReferenceCrc(new byte[] { 0x23, 0 }.Concat(frame.Data.Skip(1)), 0x1D, 0, 0), frame.Data[0]);

            DbcReadSetting read = capture ? new DbcCaptureSetting { DurationMs = 60, MaxFrames = 2 } : new DbcReadSetting();
            read.Document = Document(); read.UseMessageList = false; read.MessageId = 0x123; read.ResultVariable = "Locals.Result";
            read.Protection = write.Protection.Copy();
            // Receive processing must use the on-wire value, not local sender state/rules.
            read.Protection.RcInitialValue = 99; read.Protection.RcMinimum = 99; read.Protection.RcMaximum = 0;
            var (readContext, readState) = Context(capture ? new DbcCapturePlugin() : new DbcReadPlugin(), read, owner);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (attempt == 1)
                {
                    if (mode == CanCrcDataIdMode.Profile2) read.Protection.CrcDataIdTable[14] = "EE";
                    else read.Protection.CrcProfile1DataIdHex = "0124";
                }
                var receiving = readState.Plugin.CreateExecutor().ExecuteAsync(readContext);
                await Until(() => Router(owner).CaptureCount == 1);
                Router(owner).Publish(new() { Id = frame.Id, Data = frame.Data, TimestampNs = 456 });
                Completed(await receiving); Assert.Equal(0, Router(owner).CaptureCount);
                using var json = JsonDocument.Parse((string)readState.Variables["Locals.Result"]!);
                var decoded = capture ? json.RootElement.GetProperty("frames")[0] : json.RootElement.GetProperty("frame");
                Assert.Equal(attempt == 0, decoded.GetProperty("integrity").GetProperty("crcValid").GetBoolean());
                Assert.Equal("14", decoded.GetProperty("integrity").GetProperty("rcValue").GetString());
                Assert.Equal(Convert.ToHexString(original), decoded.GetProperty("data").GetString());
                var voltage = decoded.GetProperty("signals").EnumerateArray().Single(s => s.GetProperty("name").GetString() == "Voltage");
                Assert.Equal(12.5m, voltage.GetProperty("physicalValue").GetDecimal());
                Assert.Equal(original, frame.Data);
            }
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Dbc_cyclic_Profile1_nibble_preserves_data_and_wraps_14_to_zero_after_restart()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var initial = DbcWrite(CanCrcDataIdMode.Profile1Nibble);
        var setting = new DbcCyclicWriteSetting { UseMessageList = false, Document = Document(), MessageId = 0x123,
            Signals = initial.Signals, Protection = initial.Protection, CycleTimeMs = 5 };
        var (context, state) = Context(new DbcCyclicWritePlugin(), setting, owner);
        try
        {
            for (int restart = 0; restart < 2; restart++)
            {
                int offset = raw.Sent.Count; Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
                await Until(() => raw.Sent.Count >= offset + 3);
                await owner.StopJobAsync("DbcTask1", 1000, default);
                var sent = raw.Sent.Skip(offset).ToArray();
                Assert.Equal(new byte[] { 0x1E, 0x10, 0x11 }, sent.Take(3).Select(frame => frame.Data[1]));
                Assert.All(sent, frame =>
                {
                    Assert.Equal(new byte[] { 125, 0 }, frame.Data[2..4]);
                    Assert.Equal(ReferenceCrc(new byte[] { 0x23, 0 }.Concat(frame.Data.Skip(1)), 0x1D, 0, 0), frame.Data[0]);
                });
                int stopped = raw.Sent.Count; await Task.Delay(25); Assert.Equal(stopped, raw.Sent.Count);
            }
        }
        finally { await owner.CloseAsync(); }
    }

    private static DbcDocumentSetting Document() => new() { Name = "e2e-integration.dbc", Text = """
        BO_ 291 Sample: 8 Node
         SG_ Checksum : 0|8@1+ (1,0) [0|255] "" Receiver
         SG_ Counter : 8|4@1+ (1,0) [0|15] "" Receiver
         SG_ Sideband : 12|4@1+ (1,0) [0|15] "" Receiver
         SG_ Voltage : 16|16@1+ (0.1,0) [0|6000] "V" Receiver
        """ };

    private static DbcWriteSetting DbcWrite(CanCrcDataIdMode mode) => new()
    {
        UseMessageList = false, Document = Document(), MessageId = 0x123, Protection = Protection(mode: mode), Signals =
        [
            new() { Name = "Voltage", Value = "12.5" },
            // NIBBLE owns this field; its unused expression must not be evaluated.
            new() { Name = "Sideband", Value = mode == CanCrcDataIdMode.Profile1Nibble ? "INVALID_EXPRESSION" : "13" }
        ]
    };

    private static void AssertP2(byte[] data, string[] table)
    {
        Assert.Equal(8, data.Length);
        byte id = byte.Parse(table[data[1] & 15], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        Assert.Equal(ReferenceCrc(data.Skip(1).Append(id), 0x2F, 0xFF, 0xFF), data[0]);
    }

    // Independent small reference; expected bytes never call the production processor.
    private static byte ReferenceCrc(IEnumerable<byte> input, byte polynomial, byte initial, byte xor)
    {
        byte crc = initial;
        foreach (byte value in input)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ polynomial : crc << 1);
        }
        return (byte)(crc ^ xor);
    }
}
