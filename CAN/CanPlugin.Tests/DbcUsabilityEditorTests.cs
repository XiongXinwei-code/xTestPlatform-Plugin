using System.Reflection;
using CAN.Dbc;
using CAN.Models;
using CAN.UI.Dbc;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcUsabilityEditorTests
{
    private const string DocumentText = DbcEditorTests.DocumentText + """

        BA_DEF_ BO_ "GenMsgCycleTime" INT 0 10000;
        BA_ "GenMsgCycleTime" BO_ 291 20;
        """;
    private static DbcDocumentSetting Document() => new() { Name = "periods.dbc", Text = DocumentText };
    private static Step Step(IStepPlugin plugin, object setting)
    {
        var step = new Step(); var serializer = plugin.CreateSerializer();
        step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion;
        return step;
    }
    private static T Read<T>(IStepPlugin plugin, Step step) => (T)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_cycle_save_reentry_keeps_auto_mode_and_per_ID_periods(bool multiple)
    {
        var plugin = new DbcCyclicWritePlugin(); var serializer = plugin.CreateSerializer();
        var initial = new DbcCyclicWriteSetting { Document = Document(), MessageId = 0x123, CycleTimeMs = 75,
            UseMessageList = multiple, UseDbcCycleTime = false,
            Messages = [new() { MessageId = 0x123, UseDbcCycleTime = false, CycleTimeMs = 75 },
                new() { MessageId = 0x123, FrameType = CanFrameType.Extended, UseDbcCycleTime = false, CycleTimeMs = 75 }] };
        var step = Step(plugin, initial);
        var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        Assert.False(model.UseDbcCycleTime); Assert.Equal(75, model.CycleTimeMs);
        Assert.All(model.ConfiguredMessages, row => { Assert.False(row.UseDbcCycleTime); Assert.Equal(75, row.CycleTimeMs); });
        model.UseDbcCycleTime = true; model.CycleTimeMs = 42;
        if (multiple) model.ConfiguredMessages[1].CycleTimeMs = 64;

        // Match EditorSaveScheduler's exact publication order, with the host's
        // synchronous RefreshFromStep between the bytes and the version update.
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var scheduler = typeof(DbcEditorViewModel).GetField("_save", fields)!.GetValue(model)!;
        bool refreshed = false;
        Action publish = () =>
        {
            var setting = typeof(DbcEditorViewModel).GetField("_setting", fields)!.GetValue(model)!;
            step.StepSetting.Setting = serializer.Serialize(setting);
            Assert.Equal(serializer.SettingVersion, step.StepSetting.SettingVersion);
            model.Attach(step); refreshed = true;
            Assert.True(model.UseDbcCycleTime); Assert.Equal(42, model.CycleTimeMs);
            step.StepSetting.SettingVersion = serializer.SettingVersion;
        };
        scheduler.GetType().GetMethod("Queue")!.Invoke(scheduler, [publish]);
        model.Flush(); await model.LoadTask;
        Assert.True(refreshed); Assert.True(model.UseDbcCycleTime); Assert.Equal(42, model.CycleTimeMs);
        if (multiple)
        {
            Assert.False(model.ConfiguredMessages[1].UseDbcCycleTime);
            Assert.Equal(64, model.ConfiguredMessages[1].CycleTimeMs);
        }
        model.TimeoutMs = 5001; model.Flush();
        var saved = Read<DbcCyclicWriteSetting>(plugin, step);
        Assert.Equal(plugin.CreateSerializer().SettingVersion, step.StepSetting.SettingVersion);
        if (multiple)
        {
            Assert.True(saved.Messages[0].UseDbcCycleTime); Assert.Equal(42, saved.Messages[0].CycleTimeMs);
            Assert.False(saved.Messages[1].UseDbcCycleTime); Assert.Equal(64, saved.Messages[1].CycleTimeMs);
        }
        else { Assert.True(saved.UseDbcCycleTime); Assert.Equal(42, saved.CycleTimeMs); }
        model.Deactivate();
    }

    [WpfFact]
    public async Task Protection_owner_and_summary_follow_the_selected_ID_without_cross_editing()
    {
        var plugin = new DbcWritePlugin();
        var step = Step(plugin, new DbcWriteSetting { Document = Document(), UseMessageList = true, Messages =
        [new() { MessageId = 0x123 }, new() { MessageId = 0x123, FrameType = CanFrameType.Extended }] });
        var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        Assert.Contains("Sample", model.ActiveMessageTitle);
        model.Protection!.RcEnabled = true; model.Protection.RcStartByte = 6; model.Protection.RcEndByte = 6;
        model.Protection.CrcEnabled = true; model.Protection.CrcStartByte = 7; model.Protection.CrcEndByte = 7;
        Assert.Contains("RC：字节 6～6", model.ActiveProtectionSummary);
        Assert.Contains("CRC：字节 7～7", model.ConfiguredMessages[0].ProtectionSummary);
        model.SelectedConfiguredMessage = model.ConfiguredMessages[1];
        Assert.Contains("Extended", model.ActiveMessageTitle);
        Assert.Contains("扩展", model.ActiveMessageTitle);
        Assert.Contains("未启用", model.ActiveProtectionSummary);
        Assert.False(model.Protection!.RcEnabled);
        model.Protection.CrcEnabled = true; model.Protection.CrcStartByte = 10; model.Protection.CrcEndByte = 10;
        model.SelectedConfiguredMessage = model.ConfiguredMessages[0];
        Assert.Equal(7, model.Protection!.CrcStartByte);
        model.Flush();
        var saved = Read<DbcWriteSetting>(plugin, step);
        Assert.Equal(7, saved.Messages[0].Protection.CrcStartByte);
        Assert.Equal(10, saved.Messages[1].Protection.CrcStartByte);
        Assert.False(saved.Messages[1].Protection.RcEnabled);
        model.Deactivate();
    }

    [WpfFact]
    public async Task Per_ID_cycle_editor_shows_DBC_source_and_keeps_manual_overrides_independent()
    {
        var plugin = new DbcCyclicWritePlugin();
        var step = Step(plugin, new DbcCyclicWriteSetting { UseMessageList = false, Document = Document(), MessageId = 0x123 });
        var original = step.StepSetting.Setting.ToArray();
        var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask; model.Flush();
        Assert.Equal(original, step.StepSetting.Setting);
        Assert.True(model.UseDbcCycleTime);
        Assert.Contains("20 ms", model.ActiveTimingSummary); Assert.Contains("GenMsgCycleTime", model.ActiveTimingSummary);
        model.UseDbcCycleTime = false;
        Assert.Equal(20, model.CycleTimeMs);
        model.CycleTimeMs = 35; model.UseMessageList = true;
        Assert.False(model.UseDbcCycleTime); Assert.Equal(35, model.CycleTimeMs);
        model.MessageToAdd = model.Messages.Single(m => m.Message.Extended); model.AddSelectedMessage();
        Assert.True(model.UseDbcCycleTime);
        Assert.Contains("未声明有效周期", model.ActiveTimingSummary);
        model.CycleTimeMs = 60;
        Assert.Contains("60 ms", model.ActiveTimingSummary);
        model.ConfiguredMessages[0].UseDbcCycleTime = true;
        Assert.Contains("20 ms", model.ConfiguredMessages[0].TimingSummary);
        Assert.Contains("60 ms", model.ConfiguredMessages[1].TimingSummary);
        model.Flush();
        var saved = Read<DbcCyclicWriteSetting>(plugin, step);
        Assert.True(saved.Messages[0].UseDbcCycleTime); Assert.Equal(35, saved.Messages[0].CycleTimeMs);
        Assert.True(saved.Messages[1].UseDbcCycleTime); Assert.Equal(60, saved.Messages[1].CycleTimeMs);
        model.Deactivate();
        var restored = new DbcEditorViewModel(plugin); restored.Attach(step); await restored.LoadTask;
        Assert.Contains("20 ms", restored.ActiveTimingSummary);
        restored.ConfiguredMessages[0].UseDbcCycleTime = false;
        Assert.Equal(20, restored.ConfiguredMessages[0].CycleTimeMs);
        restored.Deactivate();
    }

    [WpfFact]
    public async Task Read_signal_selection_is_lazy_and_persists_per_ID_without_erasing_saved_mapping()
    {
        var plugin = new DbcReadPlugin();
        var step = Step(plugin, new DbcReadSetting { UseMessageList = false, Document = Document(), MessageId = 0x123, ResultVariable = "Locals.Json" });
        var original = step.StepSetting.Setting.ToArray();
        var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask; model.Flush();
        Assert.Equal(original, step.StepSetting.Setting);
        Assert.All(model.SignalRows, row => Assert.True(row.OutputSelected));
        var voltage = model.SignalRows.Single(row => row.Name == "Voltage");
        voltage.OutputVariable = "Locals.Voltage"; voltage.OutputSelected = false;
        Assert.False(voltage.CanBindOutput); Assert.True(voltage.CanSelectOutput);
        model.UseMessageList = true;
        Assert.False(model.SignalRows.Single(row => row.Name == "Voltage").OutputSelected);
        model.MessageToAdd = model.Messages.Single(m => m.Message.Extended); model.AddSelectedMessage();
        Assert.True(model.SignalRows.Single(row => row.IsExtended).OutputSelected);
        model.SelectedConfiguredMessage = model.ConfiguredMessages[0];
        voltage = model.SignalRows.Single(row => row.Name == "Voltage");
        Assert.Equal("Locals.Voltage", voltage.OutputVariable);
        Assert.False(voltage.OutputSelected); model.Flush();
        var saved = Read<DbcReadSetting>(plugin, step);
        Assert.False(Assert.Single(saved.Messages[0].SignalOutputs).Selected);
        Assert.Empty(saved.Messages[1].SignalOutputs);
        voltage.OutputSelected = true; model.Flush();
        Assert.True(Assert.Single(Read<DbcReadSetting>(plugin, step).Messages[0].SignalOutputs).Selected);
        Assert.Equal("Locals.Voltage", voltage.OutputVariable);
        model.Deactivate();
    }

    [Fact]
    public async Task Deselected_mapping_is_not_validated_or_counted_as_a_result_destination()
    {
        var plugin = new DbcReadPlugin(); var editor = new DbcReadEditorPlugin();
        var setting = new DbcReadSetting { UseMessageList = false, Document = Document(), MessageId = 0x123, SignalOutputs =
            [new() { Name = "Voltage", Selected = false, TargetVariable = "Locals.Missing", ValueKind = DbcValueKind.Raw }] };
        var errors = await editor.ValidateWithContextAsync(Context(plugin, setting));
        Assert.DoesNotContain(errors, e => e.Code is "DBC_OUTPUT_TARGET" or "DBC_OUTPUT_KIND");
        Assert.Contains(errors, e => e.Code == "DBC_RESULT");
        setting.ResultVariable = "Locals.Json";
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", "")));
        Assert.DoesNotContain(errors, e => e.Code.StartsWith("DBC_OUTPUT", StringComparison.Ordinal) || e.Code == "DBC_RESULT");
        setting.SignalOutputs[0].Name = "MissingSignal";
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", "")));
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_UNKNOWN");
    }

    [Fact]
    public async Task Capture_all_rejects_saved_signal_filter_instead_of_ignoring_it()
    {
        var plugin = new DbcCapturePlugin(); var editor = new DbcCaptureEditorPlugin();
        var setting = new DbcCaptureSetting { UseMessageList = false, Document = Document(), AllDbcMessages = true, ResultVariable = "Locals.Json",
            SignalOutputs = [new() { Name = "Voltage", Selected = false }] };
        var errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", "")));
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_ALL_MODE");
    }

    [WpfFact]
    public async Task Explicit_reset_clears_signal_filters_and_mappings_for_all_IDs_but_keeps_JSON_and_protection()
    {
        var plugin = new DbcCapturePlugin();
        var step = Step(plugin, new DbcCaptureSetting { UseMessageList = false, Document = Document(), MessageId = 0x123, ResultVariable = "Locals.Json",
            SignalOutputs = [new() { Name = "Voltage", Selected = false }],
            Protection = new() { RcEnabled = true, RcStartByte = 6, RcEndByte = 6 },
            Messages = [new() { MessageId = 0x123, SignalOutputs = [new() { Name = "Mode", TargetVariable = "Locals.Modes" }] }] });
        var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        model.ResetReadSignalSettings(); model.Flush();
        var saved = Read<DbcCaptureSetting>(plugin, step);
        Assert.Empty(saved.SignalOutputs); Assert.Empty(saved.Messages[0].SignalOutputs);
        Assert.Equal("Locals.Json", saved.ResultVariable); Assert.True(saved.Protection.RcEnabled);
        Assert.All(model.SignalRows, row => { Assert.True(row.OutputSelected); Assert.Empty(row.OutputVariable); });
        model.Deactivate();
    }

    [Fact]
    public async Task Cycle_validation_uses_each_enabled_ID_mode_and_period()
    {
        var plugin = new DbcCyclicWritePlugin(); var editor = new DbcCyclicWriteEditorPlugin();
        var setting = new DbcCyclicWriteSetting { Document = Document(), UseMessageList = true, CycleTimeMs = 0, Messages =
            [new() { MessageId = 0x123, UseDbcCycleTime = false, CycleTimeMs = 35 },
             new() { MessageId = 0x123, FrameType = CanFrameType.Extended, UseDbcCycleTime = true, CycleTimeMs = 0 }] };
        var errors = await editor.ValidateWithContextAsync(Context(plugin, setting));
        Assert.Single(errors, e => e.Code == "DBC_PERIOD");
        setting.Messages[1].CycleTimeMs = 60;
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting));
        Assert.DoesNotContain(errors, e => e.Code == "DBC_PERIOD");
        setting.Messages[0].UseDbcCycleTime = false; setting.Messages[0].CycleTimeMs = -1;
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting));
        Assert.Single(errors, e => e.Code == "DBC_PERIOD");
    }

    private static StepEditorValidationContext Context(IStepPlugin plugin, object setting, params (string Path, object? Value)[] variables)
    {
        var step = Step(plugin, setting); var execution = DispatchProxy.Create<IExecutionContext, DbcTestContext>();
        var state = (DbcTestContext)(object)execution;
        foreach (var variable in variables) state.Variables[variable.Path] = variable.Value;
        return new() { Setting = step.StepSetting.Setting, CurrentStep = step, Block = [step], SequenceFile = new(),
            ExecutionContext = execution, Evaluator = ExpressionEvaluatorFactory.Default };
    }
}
