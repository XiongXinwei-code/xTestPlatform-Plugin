using CAN.Dbc;
using CAN.Models;
using CAN.UI.Dbc;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcDependentSyncTests
{
    private static (Step Step, DbcEditorViewModel Model) Create(IStepPlugin plugin)
    {
        var setting = (DbcCanSetting)plugin.CreateSerializer().CreateDefault();
        setting.Document = new() { Name = "sync.dbc", Text = "VERSION \"sync\"\nBO_ 291 Sample: 8 ECU\n SG_ A : 0|8@1+ (1,0) [0|255] \"V\" ECU\n SG_ B : 8|8@1+ (1,0) [0|255] \"A\" ECU\n" };
        setting.UseMessageList = true;
        setting.Messages = [new() { MessageId = 291, Signals = [new() { Name = "A", Selected = true, Value = "12" }], SignalOutputs = [new() { Name = "A", Selected = true, TargetVariable = "Locals.Old" }] }];
        var step = new Step(); step.StepSetting.Setting = plugin.CreateSerializer().Serialize(setting);
        step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion;
        var model = new DbcEditorViewModel(plugin); model.Attach(step); return (step, model);
    }

    [WpfTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Source_changes_refresh_bound_signal_cells(bool read)
    {
        IStepPlugin plugin = read ? new DbcReadPlugin() : new DbcWritePlugin();
        var (_, model) = Create(plugin); await model.LoadTask;
        try
        {
            var row = model.SignalRows[0]; var box = new TextBox { DataContext = row };
            box.SetBinding(TextBox.TextProperty, new Binding(read ? nameof(DbcSignalRow.OutputVariable) : nameof(DbcSignalRow.Value)) { Mode = BindingMode.OneWay });
            var item = model.ConfiguredMessages[0].Item;
            if (read) item.SignalOutputs[0].TargetVariable = "Locals.New"; else item.Signals[0].Value = "42";
            Assert.Equal(read ? "Locals.New" : "42", box.Text);
        }
        finally { model.Deactivate(); }
    }

    [WpfTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Replacing_or_clearing_source_rows_rebinds_and_old_rows_cannot_save(bool read)
    {
        IStepPlugin plugin = read ? new DbcReadPlugin() : new DbcWritePlugin();
        var (step, model) = Create(plugin); await model.LoadTask;
        try
        {
            var item = model.ConfiguredMessages[0].Item; var old = model.SignalRows[0];
            if (read) item.SignalOutputs[0] = new() { Name = "A", Selected = true, TargetVariable = "Locals.Replaced" };
            else item.Signals[0] = new() { Name = "A", Selected = true, Value = "88" };
            Assert.Equal(read ? "Locals.Replaced" : "88", read ? model.SignalRows[0].OutputVariable : model.SignalRows[0].Value);
            if (read) item.SignalOutputs.Clear(); else item.Signals.Clear();
            var current = model.SignalRows[0];
            if (read) { old.OutputVariable = "Locals.Stale"; current.OutputVariable = "Locals.Current"; }
            else { old.Value = "999"; current.Value = "33"; }
            model.Flush();
            var saved = (DbcCanSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.Equal(read ? "Locals.Current" : "33", read ? Assert.Single(saved.Messages[0].SignalOutputs).TargetVariable : Assert.Single(saved.Messages[0].Signals).Value);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Renaming_input_moves_its_values_to_matching_DBC_signal()
    {
        var (_, model) = Create(new DbcWritePlugin()); await model.LoadTask;
        try
        {
            model.ConfiguredMessages[0].Item.Signals[0].Name = "B";
            Assert.False(model.SignalRows[0].Selected);
            Assert.Equal("12", model.SignalRows[1].Value);
            Assert.True(model.SignalRows[1].Selected);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Row_frame_format_updates_active_selector()
    {
        var (_, model) = Create(new DbcWritePlugin()); await model.LoadTask;
        try
        {
            var box = new TextBox { DataContext = model };
            box.SetBinding(TextBox.TextProperty, new Binding(nameof(DbcEditorViewModel.FrameFormat)) { Mode = BindingMode.OneWay });
            model.ConfiguredMessages[0].Item.FrameFormat = DbcFrameFormat.Fd;
            Assert.Equal(((int)DbcFrameFormat.Fd).ToString(), box.Text);
        }
        finally { model.Deactivate(); }
    }

    [WpfTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Cached_editor_reactivates_after_unload_and_preserves_pending_edits(bool read)
    {
        IStepPlugin plugin = read ? new DbcReadPlugin() : new DbcWritePlugin();
        var (step, model) = Create(plugin); await model.LoadTask;

        try
        {
            var originalLoad = model.LoadTask; var oldRow = model.SignalRows[0];
            model.Activate();
            Assert.Same(originalLoad, model.LoadTask);
            Assert.Same(oldRow, model.SignalRows[0]);
            if (read) oldRow.OutputVariable = "Locals.Pending"; else oldRow.Value = "23";

            model.Deactivate();
            var afterUnload = step.StepSetting.Setting.ToArray();
            if (read) oldRow.OutputVariable = "Locals.Stale"; else oldRow.Value = "999";
            model.Flush(); Assert.Equal(afterUnload, step.StepSetting.Setting);

            model.Activate();
            await model.LoadTask;
            var row = model.SignalRows[0]; Assert.NotSame(oldRow, row);
            Assert.Equal(read ? "Locals.Pending" : "23", read ? row.OutputVariable : row.Value);
            if (read) row.OutputVariable = "Locals.Current"; else row.Value = "42";
            model.Flush();
            var saved = (DbcCanSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.Equal(read ? "Locals.Current" : "42", read ? saved.Messages[0].SignalOutputs[0].TargetVariable : saved.Messages[0].Signals[0].Value);

            if (read) oldRow.OutputVariable = "Locals.StillStale"; else oldRow.Value = "998";
            model.Flush();
            saved = (DbcCanSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.Equal(read ? "Locals.Current" : "42", read ? saved.Messages[0].SignalOutputs[0].TargetVariable : saved.Messages[0].Signals[0].Value);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Reloaded_editor_uses_current_step_bytes_and_does_not_revive_unloaded_state()
    {
        var plugin = new DbcWritePlugin(); var (step, model) = Create(plugin); await model.LoadTask;

        try
        {
            model.Deactivate();
            var external = (DbcCanSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            external.Messages[0].Signals[0].Value = "75";
            step.StepSetting.Setting = plugin.CreateSerializer().Serialize(external);
            model.ConnectionName = "\"UnloadedValue\"";
            model.Activate(); await model.LoadTask;
            Assert.Equal("75", model.SignalRows[0].Value);
            Assert.Equal(external.ConnectionName, model.ConnectionName);
        }
        finally { model.Deactivate(); }
    }

    [WpfTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Reload_keeps_current_step_and_rejects_late_document_load(bool changeStep)
    {
        var plugin = new DbcWritePlugin(); var (first, prepared) = Create(plugin); await prepared.LoadTask; prepared.Deactivate();
        var second = new Step(); second.StepSetting.Setting = first.StepSetting.Setting.ToArray(); second.StepSetting.SettingVersion = first.StepSetting.SettingVersion;
        var incoming = new TaskCompletionSource<DbcDocumentSetting>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new DbcEditorViewModel(plugin, (_, _, _) => incoming.Task);
        model.Attach(first); await model.LoadTask;
        var oldRow = model.SignalRows[0]; var oldLoad = model.LoadFileAsync("late.dbc");
        try
        {
            model.Deactivate();
            if (changeStep) model.Attach(second);
            var refreshedLoad = model.LoadTask;
            model.Activate();
            if (changeStep) Assert.Same(refreshedLoad, model.LoadTask);
            await model.LoadTask;
            incoming.SetResult(new() { Name = "late.dbc", Text = "VERSION \"late\"\nBO_ 900 Late: 8 ECU\n SG_ Wrong : 0|8@1+ (1,0) [0|255] \"\" ECU\n" });
            await oldLoad;
            Assert.Equal("sync.dbc", model.DocumentName);
            Assert.Equal("A", model.SignalRows[0].Name);
            oldRow.Value = "999";
            model.SignalRows[0].Value = "64"; model.Flush();
            var original = (DbcCanSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, first.StepSetting.SettingVersion);
            var target = changeStep ? second : first;
            var saved = (DbcCanSetting)plugin.CreateSerializer().Deserialize(target.StepSetting.Setting, target.StepSetting.SettingVersion);
            Assert.Equal(changeStep ? "12" : "64", original.Messages[0].Signals[0].Value);
            Assert.Equal("64", saved.Messages[0].Signals[0].Value);
        }
        finally { model.Deactivate(); incoming.TrySetCanceled(); }
    }
}
