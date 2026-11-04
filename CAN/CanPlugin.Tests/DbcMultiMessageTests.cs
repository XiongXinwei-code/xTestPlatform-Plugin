using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CAN.Dbc;
using CAN.Models;
using CAN.Runtime;
using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcMultiMessageTests
{
    private const string DatabaseText = """
        BO_ 291 First: 4 ECU
         SG_ Value : 0|8@1+ (0.5,10) [10|137.5] "V" ECU
         SG_ Default : 8|8@1+ (1,0) [0|255] "" ECU
         SG_ Counter : 16|8@1+ (1,0) [0|255] "" ECU
         SG_ Checksum : 24|8@1+ (1,0) [0|255] "" ECU
        BO_ 292 Second: 4 ECU
         SG_ Value : 0|8@1- (1,0) [-128|127] "A" ECU
         SG_ Default : 8|8@1+ (1,0) [0|255] "" ECU
         SG_ Counter : 16|8@1+ (1,0) [0|255] "" ECU
         SG_ Checksum : 24|8@1+ (1,0) [0|255] "" ECU
        BA_ "GenSigStartValue" SG_ 291 Value 8;
        BA_ "GenSigStartValue" SG_ 291 Default 21;
        BA_ "GenSigStartValue" SG_ 292 Value -12;
        BA_ "GenSigStartValue" SG_ 292 Default 37;
        """;

    private static DbcDocumentSetting Document() => new() { Name = "multi.dbc", Text = DatabaseText };
    private static DbcMessageItem Row(uint id, string? expression = null) => new()
    {
        MessageId = id,
        Signals = expression == null ? [] : [new() { Name = "Value", Value = expression }]
    };
    private static (IExecutionContext Context, PayloadExpressionContext State) Context(IStepPlugin plugin,
        object setting, CanConnectionResource owner)
    {
        var context = DispatchProxy.Create<IExecutionContext, PayloadExpressionContext>();
        var state = (PayloadExpressionContext)(object)context;
        state.Plugin = plugin; state.Setting = setting; state.Adapter = owner;
        state.Variables["Locals.Result"] = "unchanged";
        return (context, state);
    }
    private static CanReceiveRouter Router(CanConnectionResource owner) => (CanReceiveRouter)typeof(CanConnectionResource)
        .GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static async Task Until(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(5000);
        while (!condition()) await Task.Delay(5, deadline.Token);
    }
    private static void Passed(ExecutionResult result) =>
        Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.Message);

    [Theory]
    [InlineData("INVALID_EXPRESSION")]
    [InlineData("128")]
    public async Task Write_prevalidates_every_enabled_row_before_submitting_any_frame(string secondValue)
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var setting = new DbcWriteSetting { Document = Document(), UseMessageList = true,
            Messages = [Row(0x123, "17"), Row(0x124, secondValue)] };
        var (context, state) = Context(new DbcWritePlugin(), setting, owner);
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Empty(raw.Sent);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Write_evaluates_selected_variables_and_fills_unselected_or_absent_signals_from_DBC()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var first = Row(0x123, "Locals.Target.Value");
        first.Signals.Add(new() { Name = "Default", Selected = false, Value = "INVALID_EXPRESSION", ValueKind = DbcValueKind.Raw });
        var second = Row(0x124);
        second.Signals.Add(new() { Name = "Value", Selected = false, Value = "INVALID_EXPRESSION" });
        var setting = new DbcWriteSetting { Document = Document(), UseMessageList = true,
            Messages = [first, new() { Enabled = false, MessageId = 0x777 }, second] };
        var (context, state) = Context(new DbcWritePlugin(), setting, owner);
        state.Variables["Locals.Target"] = new StrongBox<int>(17);
        try
        {
            Passed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            var sent = raw.Sent.ToArray();
            Assert.Equal(new uint[] { 0x123, 0x124 }, sent.Select(frame => frame.Id));
            Assert.Equal(new byte[] { 14, 21, 0, 0 }, sent[0].Data);
            Assert.Equal(new byte[] { 244, 37, 0, 0 }, sent[1].Data);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Read_accepts_out_of_order_IDs_only_once_and_preserves_other_readers()
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcReadSetting { Document = Document(), UseMessageList = true,
            Messages = [Row(0x123), Row(0x124)], ResultVariable = "Locals.Result" };
        var (context, state) = Context(new DbcReadPlugin(), setting, owner);
        try
        {
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context);
            await Until(() => router.CaptureCount == 1);
            using var observer = owner.StartCapture([0x124], 8, 5000);
            router.Publish(new() { Id = 0x124, FrameType = CanFrameType.Extended, Data = [99, 0, 0, 0] });
            router.Publish(new() { Id = 0x124, IsFd = true, Data = [98, 0, 0, 0] });
            router.Publish(new() { Id = 0x124, Data = [11, 0, 0, 0] });
            router.Publish(new() { Id = 0x124, Data = [22, 0, 0, 0] });
            router.Publish(new() { Id = 0x123, Data = [33, 0, 0, 0] });
            Passed(await run);
            using var json = JsonDocument.Parse((string)state.Variables["Locals.Result"]!);
            Assert.Equal(2, json.RootElement.GetProperty("count").GetInt32());
            var frames = json.RootElement.GetProperty("frames").EnumerateArray().ToArray();
            Assert.Equal(new[] { "0x124", "0x123" }, frames.Select(frame => frame.GetProperty("id").GetString()));
            Assert.Equal("0B000000", frames[0].GetProperty("data").GetString());
            Assert.Equal("21000000", frames[1].GetProperty("data").GetString());
            Assert.Equal(4, observer.Complete().Length);
            Assert.NotNull(await router.ReadRawAsync(0x123, 100, default, null));
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Read_missing_one_ID_times_out_without_publishing_partial_results()
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcReadSetting { Document = Document(), UseMessageList = true, TimeoutMs = 800,
            Messages = [Row(0x123), Row(0x124)], ResultVariable = "Locals.Result" };
        var (context, state) = Context(new DbcReadPlugin(), setting, owner);
        try
        {
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context);
            await Until(() => router.CaptureCount == 1);
            router.Publish(new() { Id = 0x123, Data = [1, 0, 0, 0] });
            router.Publish(new() { Id = 0x123, Data = [2, 0, 0, 0] });
            var result = await run;
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal("unchanged", state.Variables["Locals.Result"]);
            Assert.Equal(0, router.CaptureCount);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Read_cancel_after_first_ID_does_not_publish_and_rerun_requires_new_frames_for_both_IDs()
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcReadSetting { Document = Document(), UseMessageList = true,
            Messages = [Row(0x123), Row(0x124)], ResultVariable = "Locals.Result" };
        var (context, state) = Context(new DbcReadPlugin(), setting, owner);
        try
        {
            using var cancel = new CancellationTokenSource();
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context, cancel.Token);
            await Until(() => router.CaptureCount == 1);
            router.Publish(new() { Id = 0x123, Data = [1, 0, 0, 0] });
            cancel.Cancel();
            Assert.Equal(TestStatus.Aborted, (await run).StepResult.Status);
            Assert.Equal("unchanged", state.Variables["Locals.Result"]); Assert.Equal(0, router.CaptureCount);
            run = state.Plugin.CreateExecutor().ExecuteAsync(context);
            await Until(() => router.CaptureCount == 1);
            router.Publish(new() { Id = 0x124, Data = [2, 0, 0, 0] });
            await Task.Delay(25); Assert.False(run.IsCompleted);
            router.Publish(new() { Id = 0x123, Data = [3, 0, 0, 0] });
            Passed(await run);
            using var json = JsonDocument.Parse((string)state.Variables["Locals.Result"]!);
            Assert.Equal(new[] { "02000000", "03000000" }, json.RootElement.GetProperty("frames").EnumerateArray()
                .Select(frame => frame.GetProperty("data").GetString()));
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Capture_selected_IDs_stops_at_capacity_as_PASS_without_consuming_unrelated_frames()
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcCaptureSetting { Document = Document(), UseMessageList = true, DurationMs = 5000,
            MaxFrames = 2, Messages = [Row(0x123), Row(0x124)], ResultVariable = "Locals.Result" };
        var (context, state) = Context(new DbcCapturePlugin(), setting, owner);
        try
        {
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context);
            await Until(() => router.CaptureCount == 1);
            router.Publish(new() { Id = 0x777, Data = [99] });
            router.Publish(new() { Id = 0x123, IsFd = true, Data = [98, 0, 0, 0] });
            router.Publish(new() { Id = 0x123, Data = [1, 0, 0, 0] });
            router.Publish(new() { Id = 0x124, Data = [2, 0, 0, 0] });
            router.Publish(new() { Id = 0x123, Data = [3, 0, 0, 0] });
            var result = await run.WaitAsync(TimeSpan.FromSeconds(2)); Passed(result);
            Assert.Contains("已满", result.StepResult.Value?.ToString());
            using var json = JsonDocument.Parse((string)state.Variables["Locals.Result"]!);
            Assert.True(json.RootElement.GetProperty("isFull").GetBoolean());
            Assert.Equal(2, json.RootElement.GetProperty("count").GetInt32());
            Assert.Equal(new[] { "01000000", "02000000" }, json.RootElement.GetProperty("frames").EnumerateArray()
                .Select(frame => frame.GetProperty("data").GetString()));
            Assert.Equal(0, router.CaptureCount);
            Assert.Equal(0x777u, (await router.ReadRawAsync(0x777, 100, default, null))!.Id);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Cyclic_multi_IDs_recalculate_independent_targets_and_reset_each_counter_after_confirmed_stop()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var first = Row(0x123, "Locals.FirstTarget.Value"); var second = Row(0x124, "Locals.SecondTarget.Value");
        first.Protection = Counter(7); second.Protection = Counter(100);
        first.UseDbcCycleTime = false; first.CycleTimeMs = 10;
        second.UseDbcCycleTime = false; second.CycleTimeMs = 10;
        var setting = new DbcCyclicWriteSetting { Document = Document(), UseMessageList = true,
            Messages = [first, second] };
        var (context, state) = Context(new DbcCyclicWritePlugin(), setting, owner);
        var firstTarget = new StrongBox<int>(20); var secondTarget = new StrongBox<int>(30);
        state.Variables["Locals.FirstTarget"] = firstTarget; state.Variables["Locals.SecondTarget"] = secondTarget;
        // Rows prepare independently. A submitted row changes only its own next expression value;
        // readiness may group or separate rows, so cross-ID alternation is not a pool contract.
        raw.OnWrite = frame => { if (frame.Id == 0x123) firstTarget.Value++; else if (frame.Id == 0x124) secondTarget.Value++; };
        try
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int offset = raw.Sent.Count; int initialFirst = firstTarget.Value; int initialSecond = secondTarget.Value;
                Passed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
                await Until(() => raw.Sent.Skip(offset).Count(frame => frame.Id == 0x123) >= 3 &&
                    raw.Sent.Skip(offset).Count(frame => frame.Id == 0x124) >= 3);
                await owner.StopJobAsync("DbcTask1", 1000, default);
                var frames = raw.Sent.Skip(offset).ToArray();
                Assert.All(frames, frame => Assert.Contains(frame.Id, new uint[] { 0x123, 0x124 }));
                var firstFrames = frames.Where(frame => frame.Id == 0x123).Take(3).ToArray();
                var secondFrames = frames.Where(frame => frame.Id == 0x124).Take(3).ToArray();
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    Assert.Equal((initialFirst + cycle - 10) * 2, firstFrames[cycle].Data[0]);
                    Assert.Equal(initialSecond + cycle, secondFrames[cycle].Data[0]);
                    Assert.Equal(7 + cycle, firstFrames[cycle].Data[2]);
                    Assert.Equal(100 + cycle, secondFrames[cycle].Data[2]);
                }
                int stopped = raw.Sent.Count; await Task.Delay(35); Assert.Equal(stopped, raw.Sent.Count);
            }
        }
        finally { await owner.CloseAsync(); }
    }

    private static CanPayloadProtectionSetting Counter(uint initial) => new()
    { RcEnabled = true, RcStartByte = 2, RcEndByte = 2, RcInitialValue = initial };

    [Fact]
    public async Task Cyclic_stop_waits_for_blocked_native_write_and_never_submits_the_second_frame_after_cancellation()
    {
        using var release = new ManualResetEventSlim();
        var raw = new FakeAdapter { WriteBlock = release }; var owner = new CanConnectionResource(raw);
        var setting = new DbcCyclicWriteSetting { Document = Document(), UseMessageList = true,
            Messages = [Row(0x123, "17"), Row(0x124, "18")], CycleTimeMs = 5 };
        var (context, state) = Context(new DbcCyclicWritePlugin(), setting, owner);
        Task? stopping = null;
        try
        {
            Passed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stopping = owner.StopJobAsync("DbcTask1", 5000, default);
            await Task.Delay(75);
            Assert.False(stopping.IsCompleted);
            Assert.Empty(raw.Sent);
            Assert.Throws<InvalidOperationException>(() => owner.StartJob("DbcTask1", _ => Task.CompletedTask));
            release.Set();
            await stopping.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(0x123u, Assert.Single(raw.Sent).Id);
            await Task.Delay(30);
            Assert.Single(raw.Sent);
        }
        finally
        {
            release.Set();
            if (stopping != null) await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            await owner.CloseAsync();
        }
    }

    [Fact]
    public async Task Later_cyclic_task_takes_over_shared_ID_and_counter_while_stopping_old_owner_keeps_new_owner_running()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var first = Row(0x123, "17"); first.Protection = Counter(7);
        var second = Row(0x123, "18"); second.Protection = Counter(100);
        var firstSetting = new DbcCyclicWriteSetting { Document = Document(), UseMessageList = true,
            Messages = [first], TaskName = "\"FirstTask\"", CycleTimeMs = 5 };
        var secondSetting = new DbcCyclicWriteSetting { Document = Document(), UseMessageList = true,
            Messages = [second], TaskName = "\"SecondTask\"", CycleTimeMs = 5 };
        var (context, state) = Context(new DbcCyclicWritePlugin(), firstSetting, owner);
        int Count(byte value) => raw.Sent.Count(frame => frame.Data[0] == value);
        try
        {
            Passed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            await Until(() => Count(14) >= 3);
            state.Setting = secondSetting;
            Passed(await state.Plugin.CreateExecutor().ExecuteAsync(context));
            await Until(() => Count(16) >= 3);
            await owner.StopJobAsync("FirstTask", 1000, default);
            int firstStopped = Count(14), secondRunning = Count(16);
            await Until(() => Count(16) >= secondRunning + 2);
            Assert.Equal(firstStopped, Count(14));
            await owner.StopJobAsync("SecondTask", 1000, default);
            var frames = raw.Sent.ToArray();
            Assert.Equal(Enumerable.Range(7, frames.Length).Select(value => (byte)value), frames.Select(frame => frame.Data[2]));
            int switchedAt = Array.FindIndex(frames, frame => frame.Data[0] == 16);
            Assert.True(switchedAt >= 3);
            Assert.All(frames.Take(switchedAt), frame => Assert.Equal(14, frame.Data[0]));
            Assert.All(frames.Skip(switchedAt), frame => Assert.Equal(16, frame.Data[0]));
            Assert.All(frames, frame => Assert.Equal(0x123u, frame.Id));
            int stopped = raw.Sent.Count; await Task.Delay(35); Assert.Equal(stopped, raw.Sent.Count);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public void Signal_selection_and_message_text_round_trip_complete_JSON()
    {
        var row = Row(0x123, "Locals.Target.Value"); row.Signals[0].Selected = false;
        row.Protection.CrcDataIdEnabled = true;
        using var json = JsonDocument.Parse(row.ToString());
        Assert.Equal(0x123u, json.RootElement.GetProperty("MessageId").GetUInt32());
        Assert.True(json.RootElement.GetProperty("Protection").GetProperty("CrcDataIdEnabled").GetBoolean());
        var signal = json.RootElement.GetProperty("Signals")[0];
        Assert.Equal("Locals.Target.Value", signal.GetProperty("Value").GetString());
        Assert.False(signal.GetProperty("Selected").GetBoolean());
        var restored = MessagePackSerializer.Deserialize<DbcMessageItem>(MessagePackSerializer.Serialize(row));
        Assert.False(restored.Signals[0].Selected);
    }
}
