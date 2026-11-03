using System.Reflection;
using CAN.Dbc;
using CAN.Models;
using CAN.UI;
using CAN.UI.Dbc;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanBackgroundEditorTests
{
    private static Step Step(IStepPlugin plugin, object setting)
    {
        var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion;
        return step;
    }
    private static T Read<T>(IStepPlugin plugin, Step step) => (T)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
    private static StepEditorValidationContext Context(IStepPlugin plugin, object setting, params (string Path, object? Value)[] variables)
    {
        var step = Step(plugin, setting); var execution = DispatchProxy.Create<IExecutionContext, DbcTestContext>();
        var state = (DbcTestContext)(object)execution;
        foreach (var variable in variables) state.Variables[variable.Path] = variable.Value;
        return new() { Setting = step.StepSetting.Setting, CurrentStep = step, Block = [step], SequenceFile = new(),
            ExecutionContext = execution, Evaluator = ExpressionEvaluatorFactory.Default };
    }

    [WpfFact]
    public void Raw_listener_saves_rows_intervals_and_targets_before_refresh_and_rejects_stale_row_edits()
    {
        var plugin = new CanReadBackgroundStartPlugin();
        var first = Step(plugin, new CanReadBackgroundSetting { Mappings = [new() { CanId = "\"0x123\"" }] });
        var second = Step(plugin, new CanReadBackgroundSetting { Mappings = [new() { CanId = "\"0x456\"" }] });
        var model = new CanBackgroundViewModel(plugin); model.Attach(first);
        Assert.True(model.IsStart); Assert.False(model.IsStop); Assert.False(model.IsStatus);
        model.TaskName = "\"RawRx\""; model.TimeoutMs = 673; model.PublishIntervalMs = 37; model.MaxAgeMs = 741;
        model.ResultVariable = "Locals.Json";
        var stale = Assert.Single(model.Mappings); stale.DataVariable = "Locals.Raw";
        stale.FrameType = (int)CanFrameType.Extended; stale.FrameFormat = (int)CanReceiveFrameFormat.Fd;
        model.Attach(second);
        var saved = Read<CanReadBackgroundSetting>(plugin, first);
        Assert.Equal("\"RawRx\"", saved.TaskName); Assert.Equal(673, saved.TimeoutMs);
        Assert.Equal(37, saved.PublishIntervalMs); Assert.Equal(741, saved.MaxAgeMs); Assert.Equal("Locals.Json", saved.ResultVariable);
        Assert.Equal("Locals.Raw", Assert.Single(saved.Mappings).DataVariable);
        Assert.Equal(CanFrameType.Extended, saved.Mappings[0].FrameType); Assert.Equal(CanReceiveFrameFormat.Fd, saved.Mappings[0].FrameFormat);
        var originalSecond = second.StepSetting.Setting.ToArray();
        stale.DataVariable = "Locals.Stale"; model.Flush(); Assert.Equal(originalSecond, second.StepSetting.Setting);
        model.AddMapping(); model.Mappings[1].CanId = "\"0x789\""; model.Mappings[1].Enabled = false;
        model.SelectedMapping = model.Mappings[0]; model.RemoveMapping(); model.Flush();
        var current = Assert.Single(Read<CanReadBackgroundSetting>(plugin, second).Mappings);
        Assert.Equal("\"0x789\"", current.CanId); Assert.False(current.Enabled);
        model.Deactivate();
        var reopened = new CanBackgroundViewModel(plugin); reopened.Attach(first);
        Assert.Equal("Locals.Raw", Assert.Single(reopened.Mappings).DataVariable); Assert.Equal(37, reopened.PublishIntervalMs);
        reopened.Deactivate();
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stop_and_status_editors_roundtrip_their_own_configuration(bool status)
    {
        IStepPlugin plugin = status ? new CanTaskStatusPlugin() : new CanReadBackgroundStopPlugin();
        var serializer = plugin.CreateSerializer(); var step = Step(plugin, serializer.CreateDefault());
        var model = new CanBackgroundViewModel(plugin); model.Attach(step);
        Assert.Equal(status, model.IsStatus); Assert.Equal(!status, model.IsStop); Assert.False(model.IsStart);
        model.TaskName = status ? "\"\"" : "\"RawRx\""; model.TimeoutMs = 247;
        model.ResultVariable = "Locals.Tasks"; model.IncludeInactive = false; model.IgnoreIfMissing = true; model.Flush();
        model.Attach(step);
        Assert.Equal(247, model.TimeoutMs);
        if (status) { Assert.Equal("Locals.Tasks", model.ResultVariable); Assert.False(model.IncludeInactive); }
        else Assert.True(model.IgnoreIfMissing);
        model.Deactivate();
    }

    [WpfFact]
    public void Cached_listener_editor_reentry_rebuilds_from_saved_setting_and_ignores_unloaded_edits()
    {
        var plugin = new CanReadBackgroundStartPlugin();
        var step = Step(plugin, new CanReadBackgroundSetting { Mappings = [new() { CanId = "\"0x123\"" }] });
        var model = new CanBackgroundViewModel(plugin); model.Attach(step);
        var oldRow = Assert.Single(model.Mappings); oldRow.DataVariable = "Locals.Original";
        model.Deactivate(); var saved = step.StepSetting.Setting.ToArray();
        model.ResultVariable = "Locals.LateJson"; oldRow.DataVariable = "Locals.LateRow"; model.Flush();
        Assert.Equal(saved, step.StepSetting.Setting);
        model.Activate(); model.Activate();
        var current = Assert.Single(model.Mappings); Assert.NotSame(oldRow, current);
        Assert.Equal("Locals.Original", current.DataVariable); Assert.Equal("", model.ResultVariable);
        oldRow.DataVariable = "Locals.EvenLater"; current.DataVariable = "Locals.Reentered";
        model.ResultVariable = "Locals.ReenteredJson"; model.Flush();
        var result = Read<CanReadBackgroundSetting>(plugin, step);
        Assert.Equal("Locals.Reentered", Assert.Single(result.Mappings).DataVariable);
        Assert.Equal("Locals.ReenteredJson", result.ResultVariable); model.Deactivate();
    }

    [Fact]
    public async Task Raw_validation_ignores_disabled_rows_and_rejects_duplicate_or_wrong_type_outputs()
    {
        var plugin = new CanReadBackgroundStartPlugin(); var editor = new CanReadBackgroundStartEditorPlugin();
        var setting = new CanReadBackgroundSetting { ResultVariable = "Locals.Json", Mappings =
            [new() { CanId = "\"0x123\"", DataVariable = "Locals.Raw" },
             new() { Enabled = false, CanId = "!invalid", FrameFormat = CanReceiveFrameFormat.Classic, DataVariable = "Locals.Missing" }] };
        var errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", ""), ("Locals.Raw", "")));
        Assert.DoesNotContain(errors, e => e.Code.StartsWith("CAN_BACKGROUND", StringComparison.Ordinal));
        setting.Mappings[0].DataVariable = "Locals.Json";
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", "")));
        Assert.Contains(errors, e => e.Code == "CAN_BACKGROUND_DUPLICATE_TARGET");
        setting.Mappings[0].DataVariable = "Locals.Raw";
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", ""), ("Locals.Raw", 123)));
        Assert.Contains(errors, e => e.Code == "CAN_BACKGROUND_TARGET");
        setting.Mappings[0].Enabled = false;
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", "")));
        Assert.Contains(errors, e => e.Code == "CAN_BACKGROUND_EMPTY");
    }

    [Fact]
    public async Task Raw_validation_requires_an_output_and_uses_user_configured_deadlines()
    {
        var plugin = new CanReadBackgroundStartPlugin(); var editor = new CanReadBackgroundStartEditorPlugin();
        var setting = new CanReadBackgroundSetting { TimeoutMs = 0, PublishIntervalMs = 0, MaxAgeMs = -1, ResultVariable = "", Mappings = [new()] };
        var errors = await editor.ValidateWithContextAsync(Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "CAN_BACKGROUND_OUTPUT");
        Assert.Contains(errors, e => e.Code == "CAN_BACKGROUND_TIMEOUT"); Assert.Contains(errors, e => e.Code == "CAN_BACKGROUND_INTERVAL");
        setting.TimeoutMs = 125; setting.PublishIntervalMs = 13; setting.MaxAgeMs = 19; setting.Mappings[0].DataVariable = "Locals.Raw";
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Raw", "")));
        Assert.DoesNotContain(errors, e => e.Code.StartsWith("CAN_BACKGROUND", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(CanReceiveFrameFormat.Any, CanReceiveFrameFormat.Classic, true)]
    [InlineData(CanReceiveFrameFormat.Fd, CanReceiveFrameFormat.Any, true)]
    [InlineData(CanReceiveFrameFormat.Fd, CanReceiveFrameFormat.Fd, true)]
    [InlineData(CanReceiveFrameFormat.Classic, CanReceiveFrameFormat.Fd, false)]
    public async Task Raw_literal_route_validation_detects_duplicates_and_Any_overlap_without_evaluating_dynamic_IDs(
        CanReceiveFrameFormat first, CanReceiveFrameFormat second, bool overlaps)
    {
        var plugin = new CanReadBackgroundStartPlugin(); var editor = new CanReadBackgroundStartEditorPlugin();
        var setting = new CanReadBackgroundSetting { ResultVariable = "Locals.Json", Mappings =
            [new() { CanId = "\"0x123\"", FrameFormat = first }, new() { CanId = "\"291\"", FrameFormat = second }] };
        var errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", "")));
        Assert.Equal(overlaps, errors.Any(e => e.Code == "CAN_BACKGROUND_ROUTE_OVERLAP"));
        setting.Mappings[1].FrameType = CanFrameType.Extended;
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", "")));
        Assert.DoesNotContain(errors, e => e.Code == "CAN_BACKGROUND_ROUTE_OVERLAP");
        setting.Mappings[1].CanId = "\"0x20000000\"";
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", "")));
        Assert.Contains(errors, e => e.Code == "CAN_BACKGROUND_ID");
        setting.Mappings[1].FrameType = CanFrameType.Standard;
        setting.Mappings[1].CanId = "\"0x\" + \"123\"";
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", "")));
        Assert.DoesNotContain(errors, e => e.Code is "CAN_BACKGROUND_ID" or "CAN_BACKGROUND_ROUTE_OVERLAP");
    }

    [WpfFact]
    public async Task Dbc_listener_keeps_scalar_mapping_and_background_fields_through_save_and_reentry()
    {
        var plugin = new DbcReadBackgroundStartPlugin();
        var step = Step(plugin, new DbcReadBackgroundSetting { UseMessageList = false, Document = new() { Name = "listener.dbc", Text = DbcEditorTests.DocumentText }, MessageId = 0x123 });
        var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        Assert.True(model.IsBackgroundRead); Assert.True(model.IsRead); Assert.True(model.HasTaskName);
        Assert.False(model.IsCapture); Assert.False(model.IsCyclic);
        model.TaskName = "\"SignalsRx\""; model.PublishIntervalMs = 45; model.MaxAgeMs = 345;
        model.TimeoutMs = 817; model.ResultVariable = "Locals.Json";
        model.SignalRows.Single(s => s.Name == "Voltage").OutputVariable = "Locals.Voltage";
        model.UseMessageList = true; model.Flush(); model.Deactivate();
        var saved = Read<DbcReadBackgroundSetting>(plugin, step);
        Assert.Equal("\"SignalsRx\"", saved.TaskName); Assert.Equal(45, saved.PublishIntervalMs); Assert.Equal(345, saved.MaxAgeMs); Assert.Equal(817, saved.TimeoutMs);
        Assert.Equal("Locals.Voltage", Assert.Single(Assert.Single(saved.Messages).SignalOutputs).TargetVariable);
        var reopened = new DbcEditorViewModel(plugin); reopened.Attach(step); await reopened.LoadTask;
        Assert.Equal("Locals.Voltage", reopened.SignalRows.Single(s => s.Name == "Voltage").OutputVariable);
        Assert.Equal(45, reopened.PublishIntervalMs); Assert.Equal(345, reopened.MaxAgeMs); reopened.Deactivate();
    }

    [Fact]
    public async Task Dbc_listener_validation_uses_scalar_destinations_and_validates_background_configuration()
    {
        var plugin = new DbcReadBackgroundStartPlugin(); var editor = new DbcReadBackgroundStartEditorPlugin();
        var setting = new DbcReadBackgroundSetting { UseMessageList = false, Document = new() { Name = "listener.dbc", Text = DbcEditorTests.DocumentText }, MessageId = 0x123,
            SignalOutputs = [new() { Name = "Voltage", TargetVariable = "Locals.Voltage" }] };
        var errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Voltage", 0d)));
        Assert.DoesNotContain(errors, e => e.Code.StartsWith("DBC_", StringComparison.Ordinal));
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Voltage", new List<double>())));
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_TARGET");
        setting.TaskName = "\"\""; setting.PublishIntervalMs = 0;
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Voltage", 0d)));
        Assert.Contains(errors, e => e.Code == "DBC_BACKGROUND_TASK"); Assert.Contains(errors, e => e.Code == "DBC_BACKGROUND_INTERVAL");
    }
}
