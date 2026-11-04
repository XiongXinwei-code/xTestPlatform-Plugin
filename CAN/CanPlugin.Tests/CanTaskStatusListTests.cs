using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using CAN.UI;
using CAN.UI.ViewModels;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

// Retain the fixture entry point used by the actual-page runner; output is now JSON only.
[Collection("CAN blocked I/O")]
public sealed class CanTaskStatusListTests
{
    private static CanCyclicDefinition Definition(uint id, bool enabled = false, bool fault = false, CanFrameType frameType = CanFrameType.Standard) => new(id, frameType, false,
        new(enabled, 100), 0, 1000, new(), (_, check) => { check(); if (fault) throw new InvalidOperationException("fake task fault"); return Task.FromResult(new CanMessage { Id = id, Data = [1] }); },
        (_, _) => Task.FromResult(new CanCyclicControl(enabled, 100)), () => { }, () => Task.CompletedTask);

    private static (IExecutionContext Context, StatusMutationContext State, CanConnectionResource Owner) Setup(string declared = "String")
    {
        var context = DispatchProxy.Create<IExecutionContext, StatusMutationContext>();
        var state = (StatusMutationContext)(object)context;
        var owner = new CanConnectionResource(new FakeAdapter()); state.Adapter = owner;
        state.Plugin = new CanTaskStatusPlugin(); state.DeclareNull("Tasks", declared);
        state.Setting = new CanTaskStatusSetting { ResultVariable = "Locals.Tasks" };
        return (context, state, owner);
    }

    [Fact]
    public async Task Empty_connection_outputs_JSON_with_empty_arrays_and_replaces_old_string()
    {
        var (context, state, owner) = Setup(); state.Variables["Locals.Tasks"] = "old";
        try
        {
            Assert.Equal(TestStatus.Passed, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            using var json = JsonDocument.Parse(Assert.IsType<string>(state.Variables["Locals.Tasks"]));
            Assert.Empty(json.RootElement.GetProperty("SendTasks").EnumerateArray());
            Assert.Empty(json.RootElement.GetProperty("ReceiveTasks").EnumerateArray()); Assert.Single(state.Writes);
            Assert.Equal(owner.Epoch, json.RootElement.GetProperty("ConnectionEpoch").GetGuid());
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Actual_snapshot_matches_documented_complete_JSON_shape_and_ID_types()
    {
        var (context, state, owner) = Setup(); state.Variables["Locals.Latest"] = "";
        try
        {
            owner.UpdateCyclicPool("Environment", "CAN", [Definition(0x123), Definition(0x123, frameType: CanFrameType.Extended),
                Definition(0x1ABCDE, frameType: CanFrameType.Extended), Definition(0)], () => { });
            owner.UpdateCyclicPool("second", "DBC", [Definition(0x456)], () => { });
            var lease = CanBackgroundOutputLease.Acquire(context, new CanExecutionGuard(context, default), ["Locals.Latest"]);
            await owner.StartBackgroundReadAsync("Feedback", [new(0x321, CanFrameType.Standard, CanReceiveFrameFormat.Any, 1000)], 20, 1000, default, lease, (_, _) => Task.CompletedTask, "CAN");
            Assert.Equal(TestStatus.Passed, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            using var actual = JsonDocument.Parse(Assert.IsType<string>(state.Variables["Locals.Tasks"]));
            var example = Regex.Match(state.Plugin.Description, @"```json\s*([\s\S]*?)\s*```"); Assert.True(example.Success);
            using var documented = JsonDocument.Parse(example.Groups[1].Value);
            SameShape(documented.RootElement, actual.RootElement);
            var send = actual.RootElement.GetProperty("SendTasks"); Assert.Equal(2, send.GetArrayLength());
            var first = send[0]; Assert.Equal("Environment", first.GetProperty("TaskName").GetString());
            var message = first.GetProperty("Messages")[0]; Assert.Equal("0x123", message.GetProperty("Id").GetString());
            var rows = first.GetProperty("Messages");
            Assert.Equal("0x123", rows[1].GetProperty("Id").GetString()); Assert.Equal("Extended", rows[1].GetProperty("FrameType").GetString());
            Assert.Equal("0x1ABCDE", rows[2].GetProperty("Id").GetString()); Assert.Equal("0x0", rows[3].GetProperty("Id").GetString());
            using var internalSnapshot = JsonDocument.Parse(JsonSerializer.Serialize(owner.GetCyclicStatuses()));
            Assert.Equal(0x1ABCDEu, internalSnapshot.RootElement[0].GetProperty("Messages")[2].GetProperty("Id").GetUInt32());
            Assert.Equal(1, message.GetProperty("Row").GetInt32()); Assert.Equal("Paused", message.GetProperty("State").GetString());
            Assert.True(message.GetProperty("Owned").GetBoolean()); Assert.False(message.GetProperty("Active").GetBoolean());
            Assert.Equal(JsonValueKind.Number, message.GetProperty("NativePending").ValueKind);
            Assert.Equal(JsonValueKind.Null, message.GetProperty("LastSubmissionUtc").ValueKind);
            Assert.False(message.TryGetProperty("IdHex", out _)); Assert.False(first.TryGetProperty("TaskKind", out _));
            var receive = actual.RootElement.GetProperty("ReceiveTasks")[0];
            Assert.Equal("0x321", receive.GetProperty("Frames")[0].GetProperty("Id").GetString());
            Assert.Equal("NeverReceived", receive.GetProperty("Frames")[0].GetProperty("Quality").GetString());
            Assert.True(receive.GetProperty("Healthy").GetBoolean()); Assert.False(receive.GetProperty("Frames")[0].GetProperty("Valid").GetBoolean());
            ((CanTaskStatusSetting)state.Setting).IncludeInactive = false;
            Assert.Equal(TestStatus.Passed, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            using var active = JsonDocument.Parse((string)state.Variables["Locals.Tasks"]!);
            Assert.Empty(active.RootElement.GetProperty("SendTasks").EnumerateArray());
            Assert.Single(active.RootElement.GetProperty("ReceiveTasks").EnumerateArray());
        }
        finally { await owner.CloseAsync(); }
    }

    private static void SameShape(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind is JsonValueKind.True or JsonValueKind.False)
        { Assert.True(actual.ValueKind is JsonValueKind.True or JsonValueKind.False); return; }
        Assert.Equal(expected.ValueKind, actual.ValueKind);
        if (expected.ValueKind == JsonValueKind.Object)
        {
            Assert.Equal(expected.EnumerateObject().Select(p => p.Name).Order(), actual.EnumerateObject().Select(p => p.Name).Order());
            foreach (var field in expected.EnumerateObject()) SameShape(field.Value, actual.GetProperty(field.Name));
        }
        else if (expected.ValueKind == JsonValueKind.Array)
        { Assert.True(expected.GetArrayLength() > 0); Assert.True(actual.GetArrayLength() > 0); SameShape(expected[0], actual[0]); }
    }

    [Theory]
    [InlineData("String", true)] [InlineData("Dynamic", false)]
    [InlineData("List", false)] [InlineData("ListDynamic", false)]
    [InlineData("ListString", false)] [InlineData("ListDouble", false)] [InlineData("Int", false)]
    public async Task JSON_target_type_is_checked_in_editor_and_execution_without_overwriting_invalid_targets(string type, bool accepted)
    {
        var (context, state, owner) = Setup(type);
        try
        {
            var step = context.CurrentStep!.Step;
            var errors = await new CanTaskStatusEditorPlugin().ValidateWithContextAsync(new()
            { Setting = step.StepSetting.Setting, CurrentStep = step, Block = [step], SequenceFile = new(), ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default });
            Assert.Equal(!accepted, errors.Any(e => e.Code == "CAN_BACKGROUND_TARGET"));
            Assert.Equal(accepted ? TestStatus.Passed : TestStatus.Error, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            if (!accepted) { Assert.Empty(state.Writes); Assert.Null(state.Variables["Locals.Tasks"]); }
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Cancellation_or_target_type_change_before_JSON_publication_prevents_write(bool cancellation)
    {
        var (context, state, owner) = Setup(); using var cancel = new CancellationTokenSource();
        state.OnResultRead = count => { if (count != 2) return; if (cancellation) cancel.Cancel(); else state.Variables["Locals.Tasks"] = new List<object>(); };
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context, cancel.Token);
            Assert.Equal(cancellation ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status); Assert.Empty(state.Writes);
        }
        finally { await owner.CloseAsync(); }
    }

    [WpfFact]
    public void JSON_result_target_survives_flush_refresh_without_a_format_switch()
    {
        var plugin = new CanTaskStatusPlugin(); var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(new CanTaskStatusSetting { ResultVariable = "Locals.InitialJson" });
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        var model = new CanBackgroundViewModel(plugin); model.Attach(step);
        model.ResultVariable = "Locals.TasksJson"; model.Flush(); model.Attach(step);
        Assert.Equal("Locals.TasksJson", model.ResultVariable); Assert.Contains("String", model.ResultHelp);
        model.Deactivate();
        var restored = Assert.IsType<CanTaskStatusSetting>(serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion));
        Assert.Equal("Locals.TasksJson", restored.ResultVariable);
        Assert.Null(typeof(CanTaskStatusSetting).GetProperty("OutputFormat"));
    }

    internal static void VerifyActualPage()
    {
        var plugin = new CanTaskStatusPlugin(); var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(new CanTaskStatusSetting { ResultVariable = "Locals.InitialJson" }); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var view = new CanBackgroundEditorView(plugin); view.RefreshFromStep(step);
        view.Measure(new Size(1000, 600)); view.Arrange(new Rect(0, 0, 1000, 600)); view.UpdateLayout();
        var format = Assert.IsType<TextBlock>(view.FindName("OutputFormatBox")); var target = Assert.IsType<TextBox>(view.FindName("ResultVariableBox"));
        Assert.Equal("JSON 字符串", format.Text); Assert.True(format.ActualWidth > 0);
        Assert.Equal("Locals.InitialJson", target.Text);
        Assert.Null(view.FindName("JsonMigrationPanel")); Assert.Null(view.FindName("MigrateStatusToJsonButton"));
        target.SetCurrentValue(TextBox.TextProperty, "Locals.CanTasksJson"); target.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        view.ViewModel.Flush(); view.RefreshFromStep(step); view.UpdateLayout();
        Assert.Equal("Locals.CanTasksJson", target.Text);
        Assert.Contains("String", Assert.IsType<TextBlock>(view.FindName("ResultHelpText")).Text);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var beforeLateCallback = step.StepSetting.Setting.ToArray();
        view.ViewModel.ResultVariable = "Locals.LateCallback"; view.ViewModel.Flush();
        Assert.Equal(beforeLateCallback, step.StepSetting.Setting);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); view.UpdateLayout();
        Assert.Equal("Locals.CanTasksJson", target.Text);
        target.SetCurrentValue(TextBox.TextProperty, "Locals.ReenteredJson"); target.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        view.ViewModel.Flush();
        var reentered = Assert.IsType<CanTaskStatusSetting>(serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion));
        Assert.Equal("Locals.ReenteredJson", reentered.ResultVariable);
        var output = Environment.GetEnvironmentVariable("CAN_STATUS_SCREENSHOT_DIR");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output); var bitmap = new RenderTargetBitmap(1000, 600, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, "CanTaskStatus-JSON.png")); png.Save(file);
        }
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var restored = Assert.IsType<CanTaskStatusSetting>(serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion));
        Assert.Equal("Locals.ReenteredJson", restored.ResultVariable);
    }
}

public class StatusMutationContext : DbcDeclaredOutputContext
{
    public Action<int>? OnResultRead { get; set; }
    private int _resultReads;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "GetVariable" && Equals(args![0], "Locals.Tasks")) OnResultRead?.Invoke(++_resultReads);
        return base.Invoke(method, args);
    }
}
