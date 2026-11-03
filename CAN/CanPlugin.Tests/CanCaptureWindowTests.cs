using CAN.Models;
using CAN.Runtime;
using CAN.UI.Views;
using System.Reflection;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanCaptureWindowTests
{
    [Fact]
    public async Task Description_output_example_matches_real_executor_output()
    {
        var description = new CanCapturePlugin().Description;
        var examples = System.Text.RegularExpressions.Regex.Matches(description, @"```json\s*(.*?)\s*```",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        var example = JsonNode.Parse(Assert.Single(examples.Cast<System.Text.RegularExpressions.Match>()).Groups[1].Value)!;
        Assert.DoesNotContain("examples/", description);
        Assert.DoesNotContain("docs/", description);
        var context = DispatchProxy.Create<IExecutionContext, CaptureContext>();
        var state = (CaptureContext)(object)context;
        var raw = new FakeAdapter();
        var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        state.Adapter = owner; state.Plugin = new CanCapturePlugin(); state.Variables["Locals.Captured"] = "";
        state.Setting = new CanCaptureSetting { DurationMs = 1000, ResultVariable = "Locals.Captured" };
        var router = (CanReceiveRouter)typeof(CanConnectionResource).GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context, timeout.Token);
            while (router.CaptureCount == 0) await Task.Delay(5, timeout.Token);
            raw.Incoming.Add(new() { Id = 0x123, Data = [1, 2, 3, 4, 5, 6, 7, 8], IngressMonotonicTicks = 1000000 });
            raw.Incoming.Add(new() { Id = 0x18FF50E5, FrameType = CanFrameType.Extended, IsFd = true,
                Data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11], TimestampNs = 123456000, RawTimestampUs = 123456,
                TimestampDomain = "ZLG.DeviceMicroseconds", ClockId = "example-zlg-clock", IngressMonotonicTicks = 1100000 });
            Assert.Equal(TestStatus.Passed, (await run).StepResult.Status);
            var actual = JsonNode.Parse((string)state.Variables["Locals.Captured"]!)!;
            Assert.True(JsonNode.DeepEquals(example, actual), "发布示例的字段、类型、大小写、载荷或时间戳与实际输出不一致");
        }
        finally { await owner.CloseAsync(); }
    }

    [WpfFact]
    public void Capture_editor_saves_and_loads_settings()
    {
        var plugin = new CanCapturePlugin(); var serializer = plugin.CreateSerializer();
        var step = new xTestPlatform.Core.SequenceModels.Step();
        step.StepSetting.Setting = serializer.Serialize(new CanCaptureSetting()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var view = new CanCaptureEditorView(); view.RefreshFromStep(step);
        view.Measure(new Size(700, 500)); view.Arrange(new Rect(0, 0, 700, 500)); view.UpdateLayout();
        view.ViewModel.FilterIds = "\"0x123,0x456\""; view.ViewModel.DurationMs = 225;
        Assert.False(((System.Windows.Controls.Control)view.FindName("ExportPathEditor")).IsEnabled);
        ((System.Windows.Controls.CheckBox)view.FindName("ExportEnabledCheckBox")).IsChecked = true;
        view.ViewModel.ExportFilePath = "Locals.ExportPath";
        view.ViewModel.OverwriteExportFile = true; view.ViewModel.ExportTimeoutMs = 1500;
        view.ViewModel.MaxFrames = 12; view.ViewModel.ResultVariable = "Locals.Captured"; view.ViewModel.Flush();
        view.RefreshFromStep(step);
        Assert.Equal("\"0x123,0x456\"", view.ViewModel.FilterIds); Assert.Equal(225, view.ViewModel.DurationMs);
        Assert.Equal(12, view.ViewModel.MaxFrames); Assert.Equal("Locals.Captured", view.ViewModel.ResultVariable);
        Assert.True(view.ViewModel.ExportEnabled);
        Assert.True(((System.Windows.Controls.Control)view.FindName("ExportPathEditor")).IsEnabled);
        Assert.Equal("Locals.ExportPath", view.ViewModel.ExportFilePath);
        Assert.True(view.ViewModel.OverwriteExportFile); Assert.Equal(1500, view.ViewModel.ExportTimeoutMs);
    }

    [Fact]
    public async Task Executor_collects_filtered_fd_frames_and_allows_restart_after_cancel()
    {
        var context = DispatchProxy.Create<IExecutionContext, CaptureContext>(); var state = (CaptureContext)(object)context;
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        state.Adapter = owner; state.Plugin = new CanCapturePlugin(); state.Variables["Locals.Captured"] = "";
        state.Setting = new CanCaptureSetting { FilterIds = "\"0x123,0x456\"", DurationMs = 250, ResultVariable = "Locals.Captured" };
        var router = (CanReceiveRouter)typeof(CanConnectionResource).GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context, timeout.Token);
            while (router.CaptureCount == 0) await Task.Delay(5, timeout.Token);
            raw.Incoming.Add(new CanMessage { Id = 0x123, IsFd = true, Data = [1, 2, 3], TimestampNs = 1234 });
            raw.Incoming.Add(new CanMessage { Id = 0x222, Data = [4] });
            Assert.Equal(TestStatus.Passed, (await run).StepResult.Status);
            using var json = JsonDocument.Parse((string)state.Variables["Locals.Captured"]!);
            Assert.Equal(1, json.RootElement.GetProperty("count").GetInt32());
            var frame = json.RootElement.GetProperty("frames")[0];
            Assert.True(frame.GetProperty("isFd").GetBoolean()); Assert.Equal("010203", frame.GetProperty("data").GetString());
            using var cancel = new CancellationTokenSource();
            var stopped = state.Plugin.CreateExecutor().ExecuteAsync(context, cancel.Token);
            while (router.CaptureCount == 0) await Task.Delay(5, timeout.Token);
            cancel.Cancel(); Assert.Equal(TestStatus.Aborted, (await stopped).StepResult.Status);
            while (router.CaptureCount != 0) await Task.Delay(5, timeout.Token);
            var again = state.Plugin.CreateExecutor().ExecuteAsync(context, timeout.Token);
            Assert.Equal(TestStatus.Passed, (await again).StepResult.Status);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Connection_close_ends_capture_without_publishing_partial_result()
    {
        var context = DispatchProxy.Create<IExecutionContext, CaptureContext>(); var state = (CaptureContext)(object)context;
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        state.Adapter = owner; state.Plugin = new CanCapturePlugin(); state.Variables["Locals.Captured"] = "original";
        state.Setting = new CanCaptureSetting { DurationMs = 10000, ResultVariable = "Locals.Captured" };
        var router = (CanReceiveRouter)typeof(CanConnectionResource).GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var run = state.Plugin.CreateExecutor().ExecuteAsync(context, timeout.Token);
        while (router.CaptureCount == 0) await Task.Delay(5, timeout.Token);
        await owner.CloseAsync();
        Assert.Equal(TestStatus.Error, (await run.WaitAsync(timeout.Token)).StepResult.Status);
        Assert.Equal("original", state.Variables["Locals.Captured"]);
        Assert.Equal(0, router.CaptureCount);
    }

    [Fact]
    public async Task Executor_reports_full_and_preserves_collected_data()
    {
        var context = DispatchProxy.Create<IExecutionContext, CaptureContext>(); var state = (CaptureContext)(object)context;
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        state.Adapter = owner; state.Plugin = new CanCapturePlugin(); state.Variables["Locals.Captured"] = "original";
        state.Setting = new CanCaptureSetting { DurationMs = 10000, MaxFrames = 1, ResultVariable = "Locals.Captured" };
        var router = (CanReceiveRouter)typeof(CanConnectionResource).GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context, timeout.Token);
            while (router.CaptureCount == 0) await Task.Delay(5, timeout.Token);
            raw.Incoming.Add(new CanMessage { Id = 1 }); raw.Incoming.Add(new CanMessage { Id = 2, IsFd = true });
            var result = await run.WaitAsync(timeout.Token);
            Assert.Equal(TestStatus.Passed, result.StepResult.Status);
            Assert.Contains("已满", result.StepResult.Value?.ToString());
            using var json = JsonDocument.Parse((string)state.Variables["Locals.Captured"]!);
            Assert.Equal(1, json.RootElement.GetProperty("count").GetInt32());
            Assert.True(json.RootElement.GetProperty("isFull").GetBoolean());
            Assert.Equal(1, json.RootElement.GetProperty("maximumFrames").GetInt32());
            Assert.Equal("0x1", json.RootElement.GetProperty("frames")[0].GetProperty("id").GetString());
            Assert.Equal(0, router.CaptureCount);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Captures_classic_fd_and_extended_without_consuming_raw_reader()
    {
        var router = new CanReceiveRouter();
        using var capture = router.StartCapture(null, 10, 1000);
        router.Publish(new() { Id = 0x123, Data = [1] });
        router.Publish(new() { Id = 0x123456, FrameType = CanFrameType.Extended, IsFd = true, Data = new byte[64] });
        var frames = capture.Complete();
        Assert.Equal(2, frames.Length); Assert.True(frames[1].IsFd); Assert.Equal(64, frames[1].Data.Length);
        Assert.NotNull(await router.ReadRawAsync(0x123, 100, default, null));
    }

    [Fact]
    public async Task Limit_completes_at_exact_count_ignores_later_frames_and_preserves_other_readers()
    {
        var router = new CanReceiveRouter();
        using var capture = router.StartCapture([0x123, 0x456], 2, 1000);
        using var other = router.StartCapture(null, 10, 1000);
        router.Publish(new() { Id = 0x111 }); router.Publish(new() { Id = 0x123, Data = [1] });
        Assert.False(capture.Finished.IsCompleted);
        router.Publish(new() { Id = 0x456, Data = [2] });
        Assert.True(capture.Finished.IsCompletedSuccessfully);
        router.Publish(new() { Id = 0x123, Data = [3] });
        var frames = capture.Complete();
        Assert.Equal(new uint[] { 0x123, 0x456 }, frames.Select(frame => frame.Id));
        Assert.Equal(4, other.Complete().Length);
        Assert.Equal(1, (await router.ReadRawAsync(0x123, 100, default, null))!.Data[0]);
        Assert.Equal(3, (await router.ReadRawAsync(0x123, 100, default, null))!.Data[0]);
    }

    [Fact]
    public void Completed_full_and_first_only_windows_are_not_retroactively_failed()
    {
        var router = new CanReceiveRouter();
        using var full = router.StartCapture(null, 1, 1000);
        using var first = router.StartCapture(null, 1, 1000, firstOnly: true);
        router.Publish(new() { Id = 0x123 });
        Assert.True(full.IsFull);
        Assert.False(first.IsFull);
        Assert.True(first.Finished.IsCompletedSuccessfully);
        router.Finish(new IOException("subsequent receive fault"));
        Assert.Equal(0x123u, Assert.Single(full.Complete()).Id);
        Assert.Equal(0x123u, Assert.Single(first.Complete()).Id);
    }

    [Fact]
    public async Task Full_capture_exports_retained_frames_with_full_indicator()
    {
        string folder = Path.Combine(Path.GetTempPath(), "can-full-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string export = Path.Combine(folder, "full.json");
        var context = DispatchProxy.Create<IExecutionContext, CaptureContext>(); var state = (CaptureContext)(object)context;
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = CanProtocolType.FD });
        state.Adapter = owner; state.Plugin = new CanCapturePlugin(); state.Variables["Locals.Captured"] = "original";
        state.Setting = new CanCaptureSetting { DurationMs = 10000, MaxFrames = 2, ResultVariable = "Locals.Captured",
            ExportEnabled = true, ExportFilePath = JsonSerializer.Serialize(export) };
        var router = (CanReceiveRouter)typeof(CanConnectionResource).GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var run = state.Plugin.CreateExecutor().ExecuteAsync(context, timeout.Token);
            while (router.CaptureCount == 0) await Task.Delay(5, timeout.Token);
            raw.Incoming.Add(new() { Id = 1, Data = [1] });
            raw.Incoming.Add(new() { Id = 2, IsFd = true, Data = [2] });
            raw.Incoming.Add(new() { Id = 3, Data = [3] });
            var result = await run.WaitAsync(timeout.Token);
            Assert.Equal(TestStatus.Passed, result.StepResult.Status);
            Assert.Contains("已满", result.StepResult.Value?.ToString());
            string content = await File.ReadAllTextAsync(export, timeout.Token);
            Assert.Equal(state.Variables["Locals.Captured"], content);
            using var json = JsonDocument.Parse(content);
            Assert.Equal(2, json.RootElement.GetProperty("count").GetInt32());
            Assert.True(json.RootElement.GetProperty("isFull").GetBoolean());
            Assert.Equal(new[] { "0x1", "0x2" }, json.RootElement.GetProperty("frames").EnumerateArray()
                .Select(frame => frame.GetProperty("id").GetString()));
        }
        finally
        {
            await owner.CloseAsync();
            foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
    }

    [Fact]
    public async Task Deadline_and_source_failure_are_reported()
    {
        var router = new CanReceiveRouter();
        using var capture = router.StartCapture(null, 10, 25);
        router.Publish(new() { Id = 1 }); await Task.Delay(80); router.Publish(new() { Id = 2 });
        Assert.Equal(1u, Assert.Single(capture.Complete()).Id);
        using var second = router.StartCapture(null, 10, 1000);
        router.Finish(new InvalidOperationException("driver failure"));
        Assert.Contains("driver failure", Assert.Throws<InvalidOperationException>(() => second.Complete()).ToString());
    }
}

public class CaptureContext : CloseContext
{
    public bool FailCapturePublication;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    {
        "SetVariable" when FailCapturePublication => throw new InvalidOperationException("simulated variable publication failure"),
        "HasVariable" => Variables.ContainsKey((string)args![0]!),
        "GetVariable" => Variables.GetValueOrDefault((string)args![0]!),
        _ => base.Invoke(method, args)
    };
}
