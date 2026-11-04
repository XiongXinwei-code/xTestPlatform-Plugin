using System.Reflection;
using System.Text;
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
public sealed class DbcExecutionTests
{
    internal const string Text = """
        VERSION "test"
        BO_ 291 Battery: 8 Node
         SG_ Voltage : 0|16@1+ (0.1,0) [0|6000] "V" Receiver
         SG_ Current : 23|16@0- (0.5,-10) [-16000|16000] "A" Receiver
         SG_ Counter : 32|8@1+ (1,0) [0|255] "" Receiver
         SG_ Checksum : 48|16@1+ (1,0) [0|65535] "" Receiver
        BO_ 2147483939 BatteryExtended: 8 Node
         SG_ Value : 0|64@1+ (1,0) [0|0] "" Receiver
        BO_ 292 ShortFd: 8 Node
         SG_ Value : 0|8@1+ (1,0) [0|255] "" Receiver
        BA_ "VFrameFormat" BO_ 292 14;
        """;
    private static DbcDocumentSetting Document() => new() { Name = "unit.dbc", Text = Text };
    private static CanPayloadProtectionSetting Protection() => new()
    {
        RcEnabled = true, RcStartByte = 4, RcEndByte = 4, RcInitialValue = 7,
        CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Crc16Modbus,
        CrcStartByte = 6, CrcEndByte = 7, CrcDataStartByte = 0, CrcDataEndByte = 5
    };
    private static DbcWriteSetting WriteSetting() => new()
    {
        UseMessageList = false, Document = Document(), MessageId = 0x123, Protection = Protection(), Signals =
        [new() { Name = "Voltage", Value = "12.5" }, new() { Name = "Current", Value = "-3", ValueKind = DbcValueKind.Raw }]
    };
    private static (IExecutionContext Context, DbcTestContext State) Context(IStepPlugin plugin, object setting, CanConnectionResource? owner = null)
    {
        var context = DispatchProxy.Create<IExecutionContext, DbcTestContext>(); var state = (DbcTestContext)(object)context;
        state.Plugin = plugin; state.Setting = setting; state.Adapter = owner; state.Variables["Locals.Result"] = "unchanged";
        return (context, state);
    }
    private static CanReceiveRouter Router(CanConnectionResource owner) => (CanReceiveRouter)typeof(CanConnectionResource)
        .GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static async Task Until(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(5000);
        while (!condition()) await Task.Delay(5, deadline.Token);
    }
    private static void Completed(ExecutionResult result) => Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());

    [Fact]
    public async Task Write_loads_embedded_DBC_and_encodes_physical_raw_RC_CRC()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var (context, state) = Context(new DbcWritePlugin(), WriteSetting(), owner);
        try
        {
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            var sent = raw.Sent.ToArray(); Assert.Equal(2, sent.Length);
            Assert.Equal(new byte[] { 125, 0, 0xFF, 0xFD, 7, 0 }, sent[0].Data.Take(6));
            Assert.Equal("7D00FFFD0700A9E2", Convert.ToHexString(sent[0].Data));
            Assert.Equal(8, sent[1].Data[4]);
            Assert.True(DbcPayload.InspectProtection(sent[0].Data, Protection()).CrcValid);
            Assert.True(DbcPayload.InspectProtection(sent[1].Data, Protection()).CrcValid);
            Assert.Equal(12.5m, DbcDatabase.Parse(Text).Find(0x123, false).Decode(sent[0].Data)[0].PhysicalValue);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Invalid_signal_or_partial_protection_never_submits()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var setting = WriteSetting(); setting.Signals[0].Value = "12.55";
        var (context, state) = Context(new DbcWritePlugin(), setting, owner);
        try
        {
            Assert.Equal(TestStatus.Error, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            setting.Signals[0].Value = "12.5"; setting.Protection.RcStartByte = 0; setting.Protection.RcEndByte = 0;
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Contains("部分覆盖", result.StepResult.Error!.Message);
            Assert.Empty(raw.Sent);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Multiplex_only_evaluates_active_signals()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var setting = new DbcWriteSetting { UseMessageList = false, MessageId = 1, Document = new() { Text = """
            BO_ 1 Muxed: 2 Node
             SG_ Selector M : 0|8@1+ (1,0) [0|255] "" Receiver
             SG_ A m0 : 8|8@1+ (1,0) [0|255] "" Receiver
             SG_ B m1 : 8|8@1+ (1,0) [0|255] "" Receiver
            """ }, Signals = [new() { Name = "Selector", Value = "1" }, new() { Name = "A", Value = "INVALID_EXPRESSION" }, new() { Name = "B", Value = "42" }] };
        var (context, state) = Context(new DbcWritePlugin(), setting, owner);
        try { Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context)); Assert.Equal(new byte[] { 1, 42 }, Assert.Single(raw.Sent).Data); }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Read_filters_complete_identity_before_capacity_and_preserves_bad_CRC()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); var router = Router(owner);
        var (context, state) = Context(new DbcReadPlugin(), new DbcReadSetting
        { UseMessageList = false, Document = Document(), MessageId = 0x123, ResultVariable = "Locals.Result", Protection = Protection() }, owner);
        try
        {
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context); await Until(() => router.CaptureCount == 1);
            for (int i = 0; i < 20; i++) router.Publish(new() { Id = 0x123, FrameType = CanFrameType.Extended, Data = new byte[8] });
            router.Publish(new() { Id = 0x123, IsFd = true, Data = new byte[8] });
            var bytes = new byte[] { 125, 0, 0xFF, 0xFD, 7, 0, 0, 0 };
            router.Publish(new() { Id = 0x123, Data = bytes, TimestampNs = 456 });
            router.Publish(new() { Id = 0x123, Data = new byte[8] });
            Completed(await run); Assert.Equal(0, router.CaptureCount);
            using var json = JsonDocument.Parse((string)state.Variables["Locals.Result"]!);
            var frame = json.RootElement.GetProperty("frame");
            Assert.Equal(Convert.ToHexString(bytes), frame.GetProperty("data").GetString());
            Assert.False(frame.GetProperty("integrity").GetProperty("crcValid").GetBoolean());
            Assert.Equal("7", frame.GetProperty("integrity").GetProperty("rcValue").GetString());
            Assert.Equal(12.5m, frame.GetProperty("signals")[0].GetProperty("physicalValue").GetDecimal());
            Assert.Equal(456UL, frame.GetProperty("timestampNs").GetUInt64());
            Assert.NotNull(await router.ReadRawAsync(0x123, 100, default, null));
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Capture_all_decodes_extended_raw64_and_short_FD_without_prior_import()
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var (context, state) = Context(new DbcCapturePlugin(), new DbcCaptureSetting
        { UseMessageList = false, Document = Document(), AllDbcMessages = true, DurationMs = 250, MaxFrames = 2, ResultVariable = "Locals.Result" }, owner);
        try
        {
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context); await Until(() => router.CaptureCount == 1);
            router.Publish(new() { Id = 0x777, Data = new byte[8] });
            router.Publish(new() { Id = 0x124, Data = new byte[8] }); // classic mismatches FD definition
            router.Publish(new() { Id = 0x123, FrameType = CanFrameType.Extended, Data = Enumerable.Repeat((byte)255, 8).ToArray() });
            router.Publish(new() { Id = 0x124, IsFd = true, Data = [12, 0, 0, 0, 0, 0, 0, 0] });
            Completed(await run);
            using var json = JsonDocument.Parse((string)state.Variables["Locals.Result"]!);
            Assert.Equal(2, json.RootElement.GetProperty("count").GetInt32());
            var frames = json.RootElement.GetProperty("frames");
            Assert.Equal(ulong.MaxValue.ToString(), frames[0].GetProperty("signals")[0].GetProperty("rawValue").GetString());
            Assert.True(frames[1].GetProperty("isFd").GetBoolean());
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_and_capture_cancel_do_not_publish_and_can_immediately_restart(bool capture)
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        IStepPlugin plugin = capture ? new DbcCapturePlugin() : new DbcReadPlugin();
        DbcReadSetting setting = capture ? new DbcCaptureSetting { DurationMs = 100 } : new DbcReadSetting();
        setting.Document = Document(); setting.UseMessageList = false; setting.MessageId = 0x123; setting.ResultVariable = "Locals.Result";
        var (context, state) = Context(plugin, setting, owner);
        try
        {
            using var stop = new CancellationTokenSource(); var cancelled = plugin.CreateExecutor().ExecuteAsync(context, stop.Token);
            await Until(() => router.CaptureCount == 1); stop.Cancel();
            Assert.Equal(TestStatus.Aborted, (await cancelled).StepResult.Status);
            Assert.Equal("unchanged", state.Variables["Locals.Result"]); Assert.Equal(0, router.CaptureCount);
            var rerun = plugin.CreateExecutor().ExecuteAsync(context); await Until(() => router.CaptureCount == 1);
            router.Publish(new() { Id = 0x123, Data = new byte[8] });
            Completed(await rerun); Assert.NotEqual("unchanged", state.Variables["Locals.Result"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Read_timeout_is_error_and_close_does_not_publish_old_results()
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcReadSetting { UseMessageList = false, Document = Document(), MessageId = 0x123, ResultVariable = "Locals.Result", TimeoutMs = 75 };
        var (context, state) = Context(new DbcReadPlugin(), setting, owner);
        try
        {
            var timeout = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, timeout.StepResult.Status); Assert.Contains("期限", timeout.StepResult.Error!.Message);
            setting.TimeoutMs = 5000;
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context); await Until(() => router.CaptureCount == 1);
            await owner.CloseAsync(); Assert.Equal(TestStatus.Error, (await run).StepResult.Status);
            Assert.Equal("unchanged", state.Variables["Locals.Result"]); Assert.Equal(0, router.CaptureCount);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Capture_full_succeeds_but_short_frames_still_fail_without_partial_results()
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcCaptureSetting { UseMessageList = false, Document = Document(), MessageId = 0x123, ResultVariable = "Locals.Result", MaxFrames = 1, DurationMs = 150 };
        var (context, state) = Context(new DbcCapturePlugin(), setting, owner);
        try
        {
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context); await Until(() => router.CaptureCount == 1);
            router.Publish(new() { Id = 0x123, Data = new byte[8] }); router.Publish(new() { Id = 0x123, Data = new byte[8] });
            Completed(await run);
            using var json = JsonDocument.Parse((string)state.Variables["Locals.Result"]!);
            Assert.True(json.RootElement.GetProperty("isFull").GetBoolean());
            Assert.Equal(1, json.RootElement.GetProperty("count").GetInt32());
            state.Variables["Locals.Result"] = "unchanged";
            run = state.Plugin.CreateExecutor().ExecuteAsync(context); await Until(() => router.CaptureCount == 1);
            router.Publish(new() { Id = 0x123, Data = [1] });
            Assert.Equal(TestStatus.Error, (await run).StepResult.Status); Assert.Equal("unchanged", state.Variables["Locals.Result"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Cyclic_FD_uses_shared_stop_and_resets_counter_on_restart()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        var initial = WriteSetting();
        var setting = new DbcCyclicWriteSetting { UseMessageList = false, Document = Document(), MessageId = 0x123, FrameFormat = DbcFrameFormat.Fd,
            Signals = initial.Signals, Protection = initial.Protection, CycleTimeMs = 5 };
        var (context, state) = Context(new DbcCyclicWritePlugin(), setting, owner);
        try
        {
            for (int i = 0; i < 2; i++)
            {
                int offset = raw.Sent.Count;
                Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
                await Until(() => raw.Sent.Count >= offset + 3);
                await owner.StopJobAsync("DbcTask1", 1000, default);
                Assert.Equal(new byte[] { 7, 8, 9 }, raw.Sent.Skip(offset).Take(3).Select(f => f.Data[4]));
                Assert.All(raw.Sent.Skip(offset), f => { Assert.True(f.IsFd); Assert.True(DbcPayload.InspectProtection(f.Data, Protection()).CrcValid); });
                int stopped = raw.Sent.Count; await Task.Delay(30); Assert.Equal(stopped, raw.Sent.Count);
            }
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Cancelled_queued_write_cannot_send_after_blocking_writer_exits()
    {
        using var release = new ManualResetEventSlim(); var raw = new FakeAdapter { WriteBlock = release }; var owner = new CanConnectionResource(raw);
        var blocked = Task.Run(() => owner.Write(new() { Id = 0x777, Data = [0] }));
        var (context, state) = Context(new DbcWritePlugin(), WriteSetting(), owner);
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)); using var stop = new CancellationTokenSource();
            var sending = state.Plugin.CreateExecutor().ExecuteAsync(context, stop.Token); await Task.Delay(75); stop.Cancel();
            Assert.Equal(TestStatus.Aborted, (await sending).StepResult.Status);
            release.Set(); await blocked; await Task.Delay(30); Assert.DoesNotContain(raw.Sent, f => f.Id == 0x123);
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context)); Assert.Equal(7, raw.Sent.Last().Data[4]);
        }
        finally { release.Set(); await blocked; await owner.CloseAsync(); }
    }

    [Fact]
    public void Encodings_and_document_limit_are_explicit()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding(936).GetBytes("BO_ 1 M: 1 N\n SG_ V : 0|8@1+ (1,0) [0|255] \"温度\" N");
        var document = DbcDocumentIO.Decode(gbk, "Auto");
        Assert.Contains("温度", document.Text); Assert.Equal("GBK", document.Encoding);
        Assert.ThrowsAny<Exception>(() => DbcDocumentIO.Decode(gbk, "UTF-8"));
        Assert.Throws<ArgumentException>(() => DbcDocumentIO.Fingerprint(new() { Text = new string('字', 3_000_000) }));
    }

    [Theory]
    [InlineData("\"18446744073709551615.0000000001\"", DbcValueKind.Raw)]
    [InlineData("\"1.00000000000000000000000000001\"", DbcValueKind.Physical)]
    [InlineData("9007199254740993.0", DbcValueKind.Raw)]
    public async Task Expression_precision_loss_is_rejected_before_write(string expression, DbcValueKind kind)
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var setting = new DbcWriteSetting { UseMessageList = false, Document = Document(), MessageId = 0x123, FrameType = CanFrameType.Extended,
            Signals = [new() { Name = "Value", Value = expression, ValueKind = kind }] };
        var (context, state) = Context(new DbcWritePlugin(), setting, owner);
        try
        {
            Assert.Equal(TestStatus.Error, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Assert.Empty(raw.Sent);
            setting.Signals[0].Value = "\"18446744073709551615\""; setting.Signals[0].ValueKind = DbcValueKind.Raw;
            Completed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            Assert.Equal(Enumerable.Repeat((byte)255, 8), Assert.Single(raw.Sent).Data);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Repeated_metadata_cannot_expand_JSON_without_limit_and_cancel_is_observed()
    {
        var value = new { frames = Enumerable.Range(0, 10).Select(i => new { value = i, unit = new string('U', 60000) }) };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DbcJson.SerializeAsync(value, default, 100000));
        Assert.Contains("未发布部分结果", error.Message);
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DbcJson.SerializeAsync(value, stop.Token));
        var small = await DbcJson.SerializeAsync(new { result = 5 }, default, 100);
        Assert.Equal("{\"result\":5}", small);
    }

    [Fact]
    public async Task Retired_execution_releases_old_subscription_without_publishing_into_rerun()
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcReadSetting { UseMessageList = false, Document = Document(), MessageId = 0x123, ResultVariable = "Locals.Result" };
        var (context, state) = Context(new DbcReadPlugin(), setting, owner);
        try
        {
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context); await Until(() => router.CaptureCount == 1);
            context.Resources.Remove("PluginCommon.ExecutionEpoch:CAN");
            Assert.Equal(TestStatus.Error, (await run).StepResult.Status); Assert.Equal(0, router.CaptureCount);
            Assert.Equal("unchanged", state.Variables["Locals.Result"]);
            context.Resources.Remove("CAN_DBC_CatalogCache");
            var next = state.Plugin.CreateExecutor().ExecuteAsync(context); await Until(() => router.CaptureCount == 1);
            router.Publish(new() { Id = 0x123, Data = new byte[8] }); Completed(await next);
        }
        finally { await owner.CloseAsync(); }
    }
}

public class DbcTestContext : StablePayloadContext
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    {
        "HasVariable" => Variables.ContainsKey((string)args![0]!),
        "GetVariable" => Variables.GetValueOrDefault((string)args![0]!),
        _ => base.Invoke(method, args)
    };
}
