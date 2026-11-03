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
public sealed class CanPayloadExpressionIntegrationTests
{
    private static byte[] Table(int first) => Enumerable.Range(first, 16).Select(value => (byte)value).ToArray();

    private static CanPayloadProtectionSetting Protection() => new()
    {
        RcEnabled = true, RcStartByte = 1, RcEndByte = 1,
        RcUseBitField = true, RcBitOffset = 0, RcBitLength = 4,
        RcInitialValue = 14, RcMinimum = 0, RcMaximum = 15, RcStep = 1, RcOverflow = CanCounterOverflow.Wrap,
        CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Crc8Autosar,
        CrcStartByte = 0, CrcEndByte = 0, CrcDataStartByte = 0, CrcDataEndByte = -1,
        CrcFieldTreatment = CanCrcFieldTreatment.Skip, CrcDataIdEnabled = true,
        CrcDataIdMode = CanCrcDataIdMode.Profile2, CrcCounterByte = 1, CrcCounterBitOffset = 0,
        CrcDataIdTableUseExpression = true, CrcDataIdTableExpression = "Locals.DataIds",
        // Running expressions must not fall back to this intentionally unusable inline table.
        CrcDataIdTable = Enumerable.Repeat("not configured", 16).ToArray()
    };

    private static (IExecutionContext Context, PayloadExpressionContext State) Context(
        IStepPlugin plugin, object setting, CanConnectionResource owner, byte[] table)
    {
        var context = DispatchProxy.Create<IExecutionContext, PayloadExpressionContext>();
        var state = (PayloadExpressionContext)(object)context;
        state.Plugin = plugin; state.Setting = setting; state.Adapter = owner;
        state.Variables["Locals.DataIds"] = table; state.Variables["Locals.Result"] = "unchanged";
        return (context, state);
    }

    private static void Completed(ExecutionResult result) =>
        Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());

    private static async Task Until(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(5000);
        while (!condition()) await Task.Delay(5, deadline.Token);
    }

    [Fact]
    public async Task Basic_write_resolves_each_invocation_and_restarts_counter_only_when_resolved_table_changes()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var setting = new CanWriteSetting { CanId = "\"0x123\"", Data = "\"00 A0 11 22 33 44 55 66\"", Protection = Protection() };
        var first = Table(1); var second = Table(0x40);
        var (context, state) = Context(new CanWritePlugin(), setting, owner, first);
        try
        {
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            // Equal bytes in a different array are the same effective configuration.
            state.Variables["Locals.DataIds"] = first.ToArray();
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            state.Variables["Locals.DataIds"] = second;
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            byte[] secondSnapshot = second.ToArray();
            Array.Fill(second, (byte)0xA5);
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));

            var sent = raw.Sent.ToArray(); Assert.Equal(4, sent.Length);
            // Existing single-write contract: changing effective RC/CRC configuration
            // starts a new counter, including a changed resolved Data ID table.
            Assert.Equal(new byte[] { 0xAE, 0xAF, 0xAE, 0xAE }, sent.Select(frame => frame.Data[1]));
            Verify(sent[0], first); Verify(sent[1], first); Verify(sent[2], secondSnapshot); Verify(sent[3], second);
            Assert.Equal(4, state.ReadCount);
            Assert.True(setting.Protection.CrcDataIdTableUseExpression);
            Assert.All(setting.Protection.CrcDataIdTable, entry => Assert.Equal("not configured", entry));
            Assert.Equal("\"00 A0 11 22 33 44 55 66\"", setting.Data);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Basic_cyclic_freezes_array_at_start_and_only_restart_reads_replacement_table()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var setting = new CanCyclicSendStartSetting { Messages =
        [new() { CanId = "\"0x123\"", Data = "\"00 A0 11 22 33 44 55 66\"", CycleTimeMs = 5, Protection = Protection() }] };
        byte[] source = Table(1), expectedFirst = source.ToArray(), replacement = Table(0x60), expectedSecond = replacement.ToArray();
        var (context, state) = Context(new CanCyclicSendStartPlugin(), setting, owner, source);
        try
        {
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            await Until(() => raw.Sent.Count >= 4);
            int beforeMutation = raw.Sent.Count;
            Array.Fill(source, (byte)0xE1); state.Variables["Locals.DataIds"] = replacement;
            await Until(() => raw.Sent.Count >= beforeMutation + 4);
            await owner.StopJobAsync("CyclicTask1", 1000, default);
            var firstRun = raw.Sent.ToArray();
            Assert.All(firstRun, frame => Verify(frame, expectedFirst));
            Assert.Equal(Enumerable.Range(0, firstRun.Length).Select(index => (byte)(0xA0 | ((14 + index) & 15))), firstRun.Select(frame => frame.Data[1]));
            Assert.Equal(1, state.ReadCount);
            int stopped = raw.Sent.Count; await Task.Delay(25); Assert.Equal(stopped, raw.Sent.Count);

            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            await Until(() => raw.Sent.Count >= stopped + 4);
            Array.Fill(replacement, (byte)0xF2);
            int beforeSecondMutation = raw.Sent.Count;
            await Until(() => raw.Sent.Count >= beforeSecondMutation + 3);
            await owner.StopJobAsync("CyclicTask1", 1000, default);
            var secondRun = raw.Sent.Skip(stopped).ToArray();
            Assert.Equal(new byte[] { 0xAE, 0xAF, 0xA0, 0xA1 }, secondRun.Take(4).Select(frame => frame.Data[1]));
            Assert.All(secondRun, frame => Verify(frame, expectedSecond));
            Assert.Equal(2, state.ReadCount);
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dbc_write_and_read_or_capture_resolve_table_and_freeze_it_for_current_operation(bool capture)
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var router = (CanReceiveRouter)typeof(CanConnectionResource).GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        var write = new DbcWriteSetting { UseMessageList = false, Document = Document(), MessageId = 0x123, Protection = Protection(), Signals =
        [new() { Name = "Sideband", Value = "13" }, new() { Name = "Voltage", Value = "12.5" }] };
        var table = Table(1); var (writeContext, writer) = Context(new DbcWritePlugin(), write, owner, table);
        try
        {
            Completed(await writer.Plugin.CreateExecutor().ExecuteAsync(writeContext));
            var sent = Assert.Single(raw.Sent); Verify(sent, table);
            Assert.Equal((byte)0xDE, sent.Data[1]); Assert.Equal(new byte[] { 125, 0 }, sent.Data[2..4]);
            var original = sent.Data.ToArray(); Assert.Equal(1, writer.ReadCount);

            DbcReadSetting read = capture ? new DbcCaptureSetting { DurationMs = 100, MaxFrames = 2 } : new DbcReadSetting();
            read.Document = Document(); read.UseMessageList = false; read.MessageId = 0x123; read.ResultVariable = "Locals.Result"; read.Protection = Protection();
            var (readContext, reader) = Context(capture ? new DbcCapturePlugin() : new DbcReadPlugin(), read, owner, table);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var reading = reader.Plugin.CreateExecutor().ExecuteAsync(readContext);
                await Until(() => router.CaptureCount == 1);
                // The first receive has already resolved its table. A subsequent
                // variable mutation affects only the next invocation, not this one.
                if (attempt == 0) Array.Fill(table, (byte)0xC7);
                router.Publish(new() { Id = 0x123, Data = sent.Data, TimestampNs = 123 });
                Completed(await reading); Assert.Equal(0, router.CaptureCount);
                using var json = JsonDocument.Parse((string)reader.Variables["Locals.Result"]!);
                var frame = capture ? json.RootElement.GetProperty("frames")[0] : json.RootElement.GetProperty("frame");
                Assert.Equal(attempt == 0, frame.GetProperty("integrity").GetProperty("crcValid").GetBoolean());
                Assert.Equal("14", frame.GetProperty("integrity").GetProperty("rcValue").GetString());
                Assert.Equal(Convert.ToHexString(original), frame.GetProperty("data").GetString());
                Assert.Equal(original, sent.Data);
            }
            Assert.Equal(2, reader.ReadCount); Assert.True(read.Protection.CrcDataIdTableUseExpression);
        }
        finally { await owner.CloseAsync(); }
    }

    private static DbcDocumentSetting Document() => new() { Name = "expression-e2e.dbc", Text = """
        BO_ 291 Sample: 8 Node
         SG_ Checksum : 0|8@1+ (1,0) [0|255] "" Receiver
         SG_ Counter : 8|4@1+ (1,0) [0|15] "" Receiver
         SG_ Sideband : 12|4@1+ (1,0) [0|15] "" Receiver
         SG_ Voltage : 16|16@1+ (0.1,0) [0|6000] "V" Receiver
        """ };

    private static void Verify(CanMessage frame, byte[] table)
    {
        Assert.Equal(0x123u, frame.Id); Assert.Equal(8, frame.Data.Length);
        byte id = table[frame.Data[1] & 15];
        byte crc = 0xFF;
        foreach (byte value in frame.Data.Skip(1).Append(id))
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ 0x2F : crc << 1);
        }
        Assert.Equal((byte)(crc ^ 0xFF), frame.Data[0]);
    }
}
