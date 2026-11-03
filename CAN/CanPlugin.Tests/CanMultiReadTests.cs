using System.Reflection;
using System.Text.Json;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using CAN.UI.ViewModels;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanMultiReadTests
{
    [Fact]
    public async Task Multiple_id_filter_preserves_unmatched_and_duplicate_frames_for_other_reads()
    {
        var inbox = new CanFrameInbox();
        inbox.Publish(new() { Id = 9, Data = [9] });
        inbox.Publish(new() { Id = 2, Data = [20] });
        inbox.Publish(new() { Id = 1, Data = [10] });
        inbox.Publish(new() { Id = 2, Data = [21] });
        Assert.Equal(20, (await inbox.ReadAnyAsync([1, 2], 100, default))!.Data[0]);
        Assert.Equal(10, (await inbox.ReadAnyAsync([1], 100, default))!.Data[0]);
        Assert.Equal(9, (await inbox.ReadAsync(null, 100, default))!.Data[0]);
        Assert.Equal(21, (await inbox.ReadAsync(null, 100, default))!.Data[0]);
    }

    [Fact]
    public async Task Each_id_outputs_associated_json_and_waits_once_per_unique_id()
    {
        var (context, state, raw, owner) = Setup(CanReadMode.EachId, "\"0x181,0x281,0x181\"");
        raw.Incoming.Add(new() { Id = 0x281, IsFd = true, Data = [2, 8, 1] });
        raw.Incoming.Add(new() { Id = 0x181, Data = [1, 8, 1] });
        try
        {
            var result = await state.Plugin!.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Passed, result.StepResult.Status);
            using var json = JsonDocument.Parse((string)state.Variables["Locals.Result"]!);
            Assert.Equal(2, json.RootElement.GetProperty("count").GetInt32());
            var frames = json.RootElement.GetProperty("frames");
            Assert.Equal("0x281", frames[0].GetProperty("id").GetString());
            Assert.True(frames[0].GetProperty("isFd").GetBoolean());
            Assert.Equal("02 08 01", frames[0].GetProperty("data").GetString());
            Assert.Equal("0x181", frames[1].GetProperty("id").GetString());
            Assert.Equal("0x281,0x181", state.Variables["Locals.Id"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task First_matching_multifilter_preserves_legacy_hex_result()
    {
        var (context, state, raw, owner) = Setup(CanReadMode.FirstMatching, "\"0x181,0x281\"");
        raw.Incoming.Add(new() { Id = 0x123, Data = [0] });
        raw.Incoming.Add(new() { Id = 0x281, Data = [0xAB, 0xCD] });
        try
        {
            Assert.Equal(TestStatus.Passed, (await state.Plugin!.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Assert.Equal("AB CD", state.Variables["Locals.Result"]);
            Assert.Equal("0x281", state.Variables["Locals.Id"]);
            Assert.Equal(0x123u, (await owner.ReadAsync(null, 100))!.Id);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Missing_id_uses_one_deadline_without_publishing_partial_result()
    {
        var (context, state, raw, owner) = Setup(CanReadMode.EachId, "\"0x181,0x281\"");
        ((CanReadSetting)state.Setting!).ReadTimeoutMs = 100;
        raw.Incoming.Add(new() { Id = 0x181, Data = [1] });
        try
        {
            var result = await state.Plugin!.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal("unchanged", state.Variables["Locals.Result"]);
            Assert.Equal("unchanged", state.Variables["Locals.Id"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Cancelled_infinite_multiread_exits_and_same_connection_can_read_again()
    {
        var (context, state, raw, owner) = Setup(CanReadMode.EachId, "\"0x181,0x281\"");
        ((CanReadSetting)state.Setting!).ReadTimeoutMs = -1;
        try
        {
            using var cancel = new CancellationTokenSource();
            var first = state.Plugin!.CreateExecutor().ExecuteAsync(context, cancel.Token);
            await raw.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            cancel.Cancel();
            Assert.Equal(TestStatus.Aborted, (await first.WaitAsync(TimeSpan.FromSeconds(3))).StepResult.Status);
            Assert.Equal("unchanged", state.Variables["Locals.Result"]);
            raw.Incoming.Add(new() { Id = 0x281, Data = [2] }); raw.Incoming.Add(new() { Id = 0x181, Data = [1] });
            Assert.Equal(TestStatus.Passed, (await state.Plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(3))).StepResult.Status);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public void Filter_validation_has_explicit_limits_and_understands_hex_decimal_and_duplicates()
    {
        Assert.Equal(new uint[] { 1, 2, 3 }, CanReadFilter.ParseIds("0x1,2;3 1"));
        Assert.Throws<ArgumentException>(() => CanReadFilter.ParseIds("0x20000000"));
        Assert.Throws<ArgumentException>(() => CanReadFilter.ParseIds(string.Join(",", Enumerable.Range(0, 129))));
        Assert.Throws<FormatException>(() => CanReadFilter.ParseIds("not-an-id"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Receive_transition_queue_obeys_deadline_or_cancellation_and_cannot_start_late(bool cancelCaller)
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var gate = (SemaphoreSlim)typeof(CanConnectionResource).GetField("_receiveModeGate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
        using var cancel = new CancellationTokenSource();
        await gate.WaitAsync();
        try
        {
            var pending = owner.ReadAnyAsync([1, 2], cancelCaller ? -1 : 50, cancel.Token);
            if (cancelCaller) cancel.Cancel();
            if (cancelCaller) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3)));
            else await Assert.ThrowsAsync<TimeoutException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.False(owner.Controlled); Assert.False(raw.ReadEntered.Task.IsCompleted);
        }
        finally { gate.Release(); }
        try
        {
            await Task.Delay(75);
            Assert.False(owner.Controlled); Assert.False(raw.ReadEntered.Task.IsCompleted);
            raw.Incoming.Add(new() { Id = 2 });
            Assert.Equal(2u, (await owner.ReadAnyAsync([1, 2], 1000, default))!.Id);
        }
        finally { await owner.CloseAsync(); }
    }

    [WpfFact]
    public void Editor_mode_roundtrips_without_changing_legacy_defaults()
    {
        var serializer = new CanReadPlugin().CreateSerializer();
        var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(new CanReadSetting()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var vm = new CanReadViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(step);
        Assert.Equal(0, vm.ReadMode);
        vm.FilterId = "\"0x181,0x281\""; vm.ReadMode = 1; vm.Flush(); vm.AttachStep(step);
        Assert.Equal(1, vm.ReadMode); Assert.Contains("JSON", vm.ResultHelp); Assert.Equal("\"0x181,0x281\"", vm.FilterId);
    }

    private static (IExecutionContext Context, CaptureContext State, FakeAdapter Raw, CanConnectionResource Owner) Setup(CanReadMode mode, string ids)
    {
        var context = DispatchProxy.Create<IExecutionContext, CaptureContext>(); var state = (CaptureContext)(object)context;
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        state.Adapter = owner; state.Plugin = new CanReadPlugin();
        state.Setting = new CanReadSetting { FilterId = ids, ReadMode = mode, ResultVariable = "Locals.Result", IdVariable = "Locals.Id", ReadTimeoutMs = 1000 };
        state.Variables["Locals.Result"] = "unchanged"; state.Variables["Locals.Id"] = "unchanged";
        return (context, state, raw, owner);
    }
}
