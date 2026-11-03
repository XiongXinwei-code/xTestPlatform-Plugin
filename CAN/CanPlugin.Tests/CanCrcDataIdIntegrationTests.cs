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
public sealed class CanCrcDataIdIntegrationTests
{
    private static CanPayloadProtectionSetting Protection(string dataId, bool after, uint initial = 3, int counterAt = 0) => new()
    {
        RcEnabled = true, RcStartByte = counterAt, RcEndByte = counterAt, RcInitialValue = initial,
        CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Crc16Modbus,
        CrcStartByte = 6, CrcEndByte = 7, CrcDataStartByte = 0, CrcDataEndByte = 5,
        CrcDataIdEnabled = true, CrcDataIdHex = dataId,
        CrcDataIdPlacement = after ? CanCrcDataIdPlacement.AfterData : CanCrcDataIdPlacement.BeforeData
    };

    private static CanWriteSetting MultiWrite() => new()
    {
        UseMessageList = true, Messages =
        [
            new() { CanId = "\"0x123\"", Data = "\"00 11 22 33 44 55 00 00\"", Protection = Protection("12 34", false) },
            new() { CanId = "\"0x124\"", Data = "\"00 11 22 33 44 55 00 00\"", UseFdFrame = true, Protection = Protection("AB CD", true, 20) }
        ]
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

    [Fact]
    public async Task Multi_write_keeps_each_Data_ID_separate_and_continues_each_counter()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        var setting = MultiWrite(); var (context, state) = Context(new CanWritePlugin(), setting, owner);
        try
        {
            for (int call = 0; call < 3; call++) Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            var sent = raw.Sent.ToArray();
            Assert.Equal(new uint[] { 0x123, 0x124, 0x123, 0x124, 0x123, 0x124 }, sent.Select(f => f.Id));
            Assert.Equal(new byte[] { 3, 4, 5 }, sent.Where(f => f.Id == 0x123).Select(f => f.Data[0]));
            Assert.Equal(new byte[] { 20, 21, 22 }, sent.Where(f => f.Id == 0x124).Select(f => f.Data[0]));
            Assert.All(sent, VerifyBasicFrame);
            Assert.All(setting.Messages, m => Assert.Equal("\"00 11 22 33 44 55 00 00\"", m.Data));
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Invalid_second_Data_ID_rejects_the_whole_batch_without_consuming_first_counter()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        var setting = MultiWrite(); setting.Messages[1].Protection.CrcDataIdHex = "not hex";
        var (context, state) = Context(new CanWritePlugin(), setting, owner);
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("Data ID", result.StepResult.Error!.Message); Assert.Empty(raw.Sent);
            setting.Messages[1].Protection.CrcDataIdHex = "AB CD";
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            Assert.Equal(new byte[] { 3, 20 }, raw.Sent.Select(f => f.Data[0]));
            Assert.All(raw.Sent, VerifyBasicFrame);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Basic_cyclic_rows_freeze_their_Data_ID_and_restart_from_initial_counter()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        var setting = new CanCyclicSendStartSetting { Messages =
        [
            new() { CanId = "\"0x123\"", Data = "\"00 11 22 33 44 55 00 00\"", CycleTimeMs = 5, Protection = Protection("12 34", false) },
            new() { CanId = "\"0x124\"", Data = "\"00 11 22 33 44 55 00 00\"", CycleTimeMs = 8, UseFdFrame = true, Protection = Protection("AB CD", true, 20) }
        ] };
        var (context, state) = Context(new CanCyclicSendStartPlugin(), setting, owner);
        try
        {
            for (int restart = 0; restart < 2; restart++)
            {
                setting.Messages[0].Protection.CrcDataIdHex = "12 34";
                setting.Messages[1].Protection.CrcDataIdHex = "AB CD";
                int offset = raw.Sent.Count;
                Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
                await Until(() => raw.Sent.Skip(offset).Count(f => f.Id == 0x123) >= 3 && raw.Sent.Skip(offset).Count(f => f.Id == 0x124) >= 3);
                int beforeMutation = raw.Sent.Count;
                setting.Messages[0].Protection.CrcDataIdHex = "invalid";
                setting.Messages[1].Protection.CrcDataIdHex = "invalid";
                await Until(() => raw.Sent.Skip(beforeMutation).Any(f => f.Id == 0x123) && raw.Sent.Skip(beforeMutation).Any(f => f.Id == 0x124));
                await owner.StopJobAsync("CyclicTask1", 1000, default);
                var sent = raw.Sent.Skip(offset).ToArray(); Assert.All(sent, VerifyBasicFrame);
                Assert.Equal(new byte[] { 3, 4, 5 }, sent.Where(f => f.Id == 0x123).Take(3).Select(f => f.Data[0]));
                Assert.Equal(new byte[] { 20, 21, 22 }, sent.Where(f => f.Id == 0x124).Take(3).Select(f => f.Data[0]));
                int stopped = raw.Sent.Count; await Task.Delay(25); Assert.Equal(stopped, raw.Sent.Count);
            }
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Dbc_write_and_read_or_capture_use_the_same_Data_ID_without_mutating_received_bytes(bool capture, bool after)
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var write = DbcWrite(after); var (writeContext, writeState) = Context(new DbcWritePlugin(), write, owner);
        var router = (CanReceiveRouter)typeof(CanConnectionResource).GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        try
        {
            Completed(await writeState.Plugin.CreateExecutor().ExecuteAsync(writeContext));
            var frame = Assert.Single(raw.Sent); AssertCrc(frame, [0x12, 0x34], after);
            Assert.Equal(new byte[] { 125, 0, 0xFF, 0xFD, 7, 0 }, frame.Data.Take(6));
            var original = frame.Data.ToArray();
            DbcReadSetting read = capture ? new DbcCaptureSetting { DurationMs = 100, MaxFrames = 2 } : new DbcReadSetting();
            read.Document = Document(); read.UseMessageList = false; read.MessageId = 0x123; read.ResultVariable = "Locals.Result"; read.Protection = write.Protection.Copy();
            var (readContext, readState) = Context(capture ? new DbcCapturePlugin() : new DbcReadPlugin(), read, owner);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (attempt == 1) read.Protection.CrcDataIdHex = "99 88";
                var receiving = readState.Plugin.CreateExecutor().ExecuteAsync(readContext);
                await Until(() => router.CaptureCount == 1);
                router.Publish(new() { Id = frame.Id, Data = frame.Data, TimestampNs = 456 });
                Completed(await receiving); Assert.Equal(0, router.CaptureCount);
                using var json = JsonDocument.Parse((string)readState.Variables["Locals.Result"]!);
                var decoded = capture ? json.RootElement.GetProperty("frames")[0] : json.RootElement.GetProperty("frame");
                Assert.Equal(attempt == 0, decoded.GetProperty("integrity").GetProperty("crcValid").GetBoolean());
                Assert.Equal(Convert.ToHexString(original), decoded.GetProperty("data").GetString());
                Assert.Equal("7", decoded.GetProperty("integrity").GetProperty("rcValue").GetString());
                Assert.Equal(12.5m, decoded.GetProperty("signals")[0].GetProperty("physicalValue").GetDecimal());
                Assert.Equal(original, frame.Data);
            }
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dbc_cyclic_write_freezes_Data_ID_and_restarts_its_counter(bool after)
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        var initial = DbcWrite(after);
        var setting = new DbcCyclicWriteSetting { UseMessageList = false, Document = Document(), MessageId = 0x123, FrameFormat = DbcFrameFormat.Fd,
            Signals = initial.Signals, Protection = initial.Protection, CycleTimeMs = 5 };
        var (context, state) = Context(new DbcCyclicWritePlugin(), setting, owner);
        try
        {
            for (int restart = 0; restart < 2; restart++)
            {
                setting.Protection.CrcDataIdHex = "12 34";
                int offset = raw.Sent.Count; Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
                await Until(() => raw.Sent.Count >= offset + 3);
                int beforeMutation = raw.Sent.Count; setting.Protection.CrcDataIdHex = "invalid";
                await Until(() => raw.Sent.Count >= beforeMutation + 2);
                await owner.StopJobAsync("DbcTask1", 1000, default);
                var sent = raw.Sent.Skip(offset).ToArray();
                Assert.Equal(new byte[] { 7, 8, 9 }, sent.Take(3).Select(f => f.Data[4]));
                Assert.All(sent, f => { Assert.True(f.IsFd); AssertCrc(f, [0x12, 0x34], after); });
                int stopped = raw.Sent.Count; await Task.Delay(25); Assert.Equal(stopped, raw.Sent.Count);
            }
        }
        finally { await owner.CloseAsync(); }
    }

    private static DbcDocumentSetting Document() => new() { Name = "data-id.dbc", Text = DbcExecutionTests.Text };
    private static DbcWriteSetting DbcWrite(bool after) => new()
    {
        UseMessageList = false, Document = Document(), MessageId = 0x123, Protection = Protection("12 34", after, 7, 4), Signals =
        [new() { Name = "Voltage", Value = "12.5" }, new() { Name = "Current", Value = "-3", ValueKind = DbcValueKind.Raw }]
    };

    private static void VerifyBasicFrame(CanMessage frame)
    {
        Assert.Equal(8, frame.Data.Length);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55 }, frame.Data.Skip(1).Take(5));
        if (frame.Id == 0x123) { Assert.False(frame.IsFd); AssertCrc(frame, [0x12, 0x34], false); }
        else { Assert.Equal(0x124u, frame.Id); Assert.True(frame.IsFd); AssertCrc(frame, [0xAB, 0xCD], true); }
    }

    // Independent reflected MODBUS reference (0xA001/right shift), not the production CRC implementation.
    private static void AssertCrc(CanMessage frame, byte[] dataId, bool after)
    {
        Assert.Equal(8, frame.Data.Length);
        var data = frame.Data.Take(6);
        var input = after ? data.Concat(dataId) : dataId.Concat(data);
        ushort expected = CanCrcDataIdTests.ReferenceModbus(input);
        Assert.Equal((byte)expected, frame.Data[6]); Assert.Equal((byte)(expected >> 8), frame.Data[7]);
    }
}
