using System.Collections.Specialized;
using CAN.Dbc;
using CAN.UI.Dbc;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class DbcEditorProjectionTests
{
    private static readonly string Catalog = string.Join('\n', Enumerable.Range(1, 256)
        .Select(id => $"BO_ {id} Frame{id}: 8 ECU\n SG_ Value : 0|8@1+ (1,0) [0|255] \"\" ECU"));
    private static Step CreateStep(DbcWritePlugin plugin, bool file)
    {
        var serializer = plugin.CreateSerializer();
        var setting = new DbcWriteSetting { DbcFilePath = file ? "D:/bench/perf.dbc" : "", UseMessageList = true,
            Messages = [new() { MessageId = 1 }], Document = new() { Text = file ? "" : Catalog } };
        var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting);
        return step;
    }

    [WpfFact]
    public async Task Catalog_publication_announces_the_complete_message_list_once()
    {
        var plugin = new DbcWritePlugin(); var model = new DbcEditorViewModel(plugin);
        int adds = 0, published = 0;
        model.Messages.CollectionChanged += (_, change) =>
        {
            if (change.Action == NotifyCollectionChangedAction.Add) adds++;
            if (change.Action == NotifyCollectionChangedAction.Reset && model.Messages.Count > 0) published++;
        };
        try
        {
            model.Attach(CreateStep(plugin, false)); await model.LoadTask;
            Assert.Empty(model.LoadError); Assert.Equal(256, model.Messages.Count);
            Assert.Equal(0, adds); Assert.Equal(1, published);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Own_save_refresh_keeps_the_inflight_file_read_and_explicit_reload_reads_new_contents()
    {
        var plugin = new DbcWritePlugin(); var step = CreateStep(plugin, true);
        var first = new TaskCompletionSource<DbcDocumentSetting>(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        var model = new DbcEditorViewModel(plugin, (_, _, _) => ++reads == 1 ? first.Task :
            Task.FromResult(new DbcDocumentSetting { Text = "BO_ 1 Changed: 8 ECU" }));
        try
        {
            model.Attach(step); var original = model.LoadTask;
            model.ExecuteCommand = (_, save) => { save(); model.Attach(step); };
            model.ConnectionName = "\"Updated\""; model.Flush();
            first.TrySetResult(new() { Text = Catalog });
            await model.LoadTask; await original;
            Assert.Equal(1, reads); Assert.Equal("Frame1", model.Messages[0].Message.Name);
            Assert.Equal("\"Updated\"", model.ConnectionName);
            await model.ReloadSourceAsync();
            Assert.Equal(2, reads); Assert.Equal("Changed", Assert.Single(model.Messages).Message.Name);
        }
        finally { first.TrySetCanceled(); model.Deactivate(); }
    }

    [WpfFact]
    public async Task An_unloaded_page_releases_catalog_projections_and_restores_the_selected_message_when_reopened()
    {
        var plugin = new DbcWritePlugin(); var step = CreateStep(plugin, false);
        var model = new DbcEditorViewModel(plugin);
        try
        {
            model.Attach(step); await model.LoadTask;
            model.AddMessages([model.Messages[5]]); model.Flush();
            Assert.Equal(6u, model.SelectedConfiguredMessage!.Item.MessageId);
            var previous = model.SignalRows[^1];
            model.Deactivate();
            Assert.Empty(model.SignalRows); Assert.Empty(model.Messages); Assert.Empty(model.ConfiguredMessages);
            var saved = step.StepSetting.Setting.ToArray(); previous.Value = "44"; model.Flush();
            model.Activate(); await model.LoadTask;
            Assert.Equal(6u, model.SelectedConfiguredMessage!.Item.MessageId);
            Assert.Equal(2, model.SignalRows.Count); Assert.NotSame(previous, model.SignalRows[^1]);
            Assert.Equal(saved, step.StepSetting.Setting);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task External_settings_written_before_the_debounced_save_are_kept_and_the_conflict_is_visible()
    {
        var plugin = new DbcWritePlugin(); var step = CreateStep(plugin, false);
        var model = new DbcEditorViewModel(plugin);
        try
        {
            model.Attach(step); await model.LoadTask;
            model.ConnectionName = "\"PendingLocal\"";
            var serializer = plugin.CreateSerializer();
            var replacement = (DbcWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            replacement.ConnectionName = "\"Assistant\""; replacement.Messages[0].MessageId = 8;
            var expected = serializer.Serialize(replacement);
            step.StepSetting.Setting = expected.ToArray(); model.Attach(step); await model.LoadTask;
            Assert.Equal(expected, step.StepSetting.Setting);
            Assert.Equal("\"Assistant\"", model.ConnectionName);
            Assert.Equal(8u, Assert.Single(model.SignalRows).MessageId);
            Assert.Contains("平台已更新步骤设置", model.LoadError);
            model.ConnectionName = "\"SubsequentEdit\""; model.Flush();
            Assert.DoesNotContain("平台已更新步骤设置", model.LoadError);
            Assert.Equal("\"SubsequentEdit\"", ((DbcWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)).ConnectionName);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Host_replacement_during_a_save_is_applied_even_for_the_same_step()
    {
        var plugin = new DbcWritePlugin(); var step = CreateStep(plugin, false);
        var model = new DbcEditorViewModel(plugin);
        try
        {
            model.Attach(step); await model.LoadTask;
            var old = Assert.Single(model.SignalRows);
            model.ExecuteCommand = (_, save) =>
            {
                save();
                var serializer = plugin.CreateSerializer();
                var replacement = (DbcWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
                replacement.Messages[0].MessageId = 255;
                step.StepSetting.Setting = serializer.Serialize(replacement);
                model.Attach(step);
            };
            model.ConnectionName = "\"Updated\""; model.Flush(); await model.LoadTask;
            Assert.Equal(255u, Assert.Single(model.SignalRows).MessageId);
            var before = step.StepSetting.Setting.ToArray(); old.Value = "999"; model.Flush();
            Assert.Equal(before, step.StepSetting.Setting);
        }
        finally { model.Deactivate(); }
    }

    [WpfTheory]
    [InlineData(0, 0, 0)] [InlineData(1, 1, 1)] [InlineData(49, 1, 49)]
    [InlineData(50, 1, 50)] [InlineData(51, 2, 1)] [InlineData(100, 2, 50)]
    public async Task Fifty_row_page_boundaries_cover_the_complete_catalog(int signals, int pages, int lastPageRows)
    {
        var plugin = new DbcWritePlugin(); var serializer = plugin.CreateSerializer();
        var setting = (DbcWriteSetting)serializer.CreateDefault();
        setting.Document.Text = "BO_ 291 Boundary: 16 ECU\n" + string.Join('\n', Enumerable.Range(0, signals)
            .Select(i => $" SG_ S{i} : {i}|1@1+ (1,0) [0|1] \"\" ECU"));
        setting.Messages.Add(new() { MessageId = 291 });
        var step = new Step(); step.StepSetting.StepType = plugin.StepTypeId; step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting); var model = new DbcEditorViewModel(plugin);
        try
        {
            model.Attach(step); await model.LoadTask; Assert.Empty(model.LoadError);
            Assert.Equal(signals, model.TotalSignalCount); Assert.Equal(pages, model.SignalPageCount);
            Assert.Equal(signals > 50, model.ShowSignalPaging); Assert.Equal(Math.Min(50, signals), model.SignalRows.Count);
            model.GoToSignalPage(int.MaxValue); Assert.Equal(pages, model.SignalPageNumber); Assert.Equal(lastPageRows, model.SignalRows.Count);
            if (signals > 0) Assert.Equal($"S{signals - 1}", model.SignalRows[^1].Name);
        }
        finally { model.Deactivate(); }
    }

    [WpfTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task All_10100_signals_can_be_paged_and_tail_edits_survive_reopen_and_assistant_updates(bool reader)
    {
        xTestPlatform.Core.Plugins.Contracts.IStepPlugin plugin = reader ? new DbcReadPlugin() : new DbcWritePlugin();
        var serializer = plugin.CreateSerializer(); var setting = (DbcCanSetting)serializer.CreateDefault();
        setting.TimeoutMs = 60000;
        var text = new System.Text.StringBuilder();
        for (uint id = 300; id < 400; id++)
        {
            text.AppendLine($"BO_ {id} Msg{id}: 16 ECU");
            var item = new DbcMessageItem { MessageId = id };
            setting.Messages.Add(item);
            for (int s = 0; s < 101; s++) text.AppendLine($" SG_ S{s} : {s}|1@1+ (1,0) [0|1] \"\" ECU");
        }
        if (reader) setting.Messages[0].SignalOutputs.Add(new() { Name = "S0", TargetVariable = "Locals.KeepFirst" });
        else setting.Messages[0].Signals.Add(new() { Name = "S0", Value = "Locals.KeepFirst" });
        setting.Document.Text = text.ToString(); setting.UseMessageList = true;
        var step = new Step(); step.StepSetting.StepType = plugin.StepTypeId; step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting); var original = step.StepSetting.Setting.ToArray();
        var model = new DbcEditorViewModel(plugin);
        try
        {
            model.Attach(step); await model.LoadTask;
            Assert.Empty(model.LoadError); Assert.Equal(10100, model.TotalSignalCount); Assert.Equal(202, model.SignalPageCount);
            var first = model.SignalRows[0]; var seen = new HashSet<(uint Id, string Name)>();
            for (int page = 1; page <= model.SignalPageCount; page++)
            {
                model.GoToSignalPage(page); Assert.Equal(page, model.SignalPageNumber);
                Assert.InRange(model.SignalRows.Count, 1, 50);
                Assert.All(model.SignalRows, row => Assert.True(seen.Add((row.MessageId, row.Name))));
            }
            Assert.Equal(10100, seen.Count); Assert.Equal(original, step.StepSetting.Setting);
            model.GoToSignalPage(1);
            model.LocateSignalMessage(model.ConfiguredMessages[^1]); Assert.Equal(200, model.SignalPageNumber);
            Assert.Contains(model.SignalRows, row => row.MessageId == 399 && row.Name == "S0");
            model.GoToSignalPage(int.MaxValue); Assert.Equal(202, model.SignalPageNumber);
            Assert.Equal(300u, model.SelectedConfiguredMessage!.Item.MessageId);
            var last = model.SignalRows[^1]; Assert.Equal(399u, last.MessageId); Assert.Equal("S100", last.Name);
            if (reader) last.OutputVariable = "Locals.Tail";
            else { last.Selected = true; last.Value = "Locals.Tail"; }
            model.GoToSignalPage(1); var saved = step.StepSetting.Setting.ToArray();
            if (reader) { first.OutputVariable = "Locals.Stale"; last.OutputVariable = "Locals.Stale"; }
            else { first.Value = "Locals.Stale"; last.Value = "Locals.Stale"; }
            model.Flush(); Assert.Equal(saved, step.StepSetting.Setting);
            model.Deactivate(); model.Activate(); await model.LoadTask;
            Assert.Equal("Locals.KeepFirst", reader ? model.SignalRows[0].OutputVariable : model.SignalRows[0].Value);
            model.GoToSignalPage(int.MaxValue); last = model.SignalRows[^1];
            Assert.Equal("Locals.Tail", reader ? last.OutputVariable : last.Value);
            var updated = (DbcCanSetting)serializer.Deserialize(saved, serializer.SettingVersion);
            Assert.Equal(100, updated.Messages.Count);
            Assert.All(updated.Messages.Skip(1).Take(98), item => { Assert.Empty(item.Signals); Assert.Empty(item.SignalOutputs); });
            if (reader) Assert.Single(updated.Messages[^1].SignalOutputs).TargetVariable = "Locals.Assistant";
            else Assert.Single(updated.Messages[^1].Signals).Value = "Locals.Assistant";
            var assistant = serializer.Serialize(updated); step.StepSetting.Setting = assistant.ToArray();
            model.Attach(step); await model.LoadTask;
            if (reader) last.OutputVariable = "Locals.Stale"; else last.Value = "Locals.Stale";
            model.Flush(); Assert.Equal(assistant, step.StepSetting.Setting);
            Assert.Equal(202, model.SignalPageNumber);
            Assert.Equal("Locals.Assistant", reader ? model.SignalRows[^1].OutputVariable : model.SignalRows[^1].Value);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Locating_duplicate_numeric_ID_uses_frame_type_and_shrinking_the_list_clamps_the_last_page()
    {
        var plugin = new DbcWritePlugin(); var serializer = plugin.CreateSerializer(); var model = new DbcEditorViewModel(plugin);
        var setting = (DbcWriteSetting)serializer.CreateDefault(); setting.UseMessageList = true;
        const uint extended = 0x80000123;
        var text = new System.Text.StringBuilder();
        foreach (var (raw, count) in new[] { (291u, 130), (extended, 130), (292u, 130), (293u, 5) })
        {
            text.AppendLine($"BO_ {raw} Msg{raw}: 32 ECU");
            for (int i = 0; i < count; i++) text.AppendLine($" SG_ S{i} : {i}|1@1+ (1,0) [0|1] \"\" ECU");
        }
        setting.Document.Text = text.ToString();
        setting.Messages = [new() { MessageId = 291 }, new() { MessageId = 291, FrameType = CAN.Models.CanFrameType.Extended }, new() { MessageId = 292 }];
        var step = new Step(); step.StepSetting.StepType = plugin.StepTypeId; step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting);
        try
        {
            model.Attach(step); await model.LoadTask; Assert.Empty(model.LoadError);
            model.LocateSignalMessage(model.ConfiguredMessages[1]); Assert.Equal(3, model.SignalPageNumber);
            Assert.Contains(model.SignalRows, s => s.IsExtended && s.MessageId == 291 && s.Name == "S0");
            model.ConfiguredMessages[1].Enabled = false;
            Assert.Equal(390, model.TotalSignalCount);
            Assert.Contains(model.SignalRows, s => s.IsExtended && s.MessageGroup.Contains("已禁用", StringComparison.Ordinal));
            model.LocateSignalMessage(model.ConfiguredMessages[0]); Assert.Equal(1, model.SignalPageNumber);
            model.GoToSignalPage(int.MaxValue); Assert.Equal(8, model.SignalPageNumber); model.RemoveMessages([model.ConfiguredMessages[2]]);
            Assert.Equal(260, model.TotalSignalCount); Assert.Equal(6, model.SignalPageNumber); Assert.Equal(10, model.SignalRows.Count);
            var edited = model.ConfiguredMessages[1].Item;
            edited.MessageId = 293; edited.FrameType = CAN.Models.CanFrameType.Standard;
            Assert.Equal(135, model.TotalSignalCount); Assert.Equal(3, model.SignalPageNumber); Assert.Equal(35, model.SignalRows.Count);
            Assert.Contains(model.SignalRows, s => !s.IsExtended && s.MessageId == 293);
        }
        finally { model.Deactivate(); }
    }

    [WpfTheory]
    [InlineData("apply")]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("list-mode")]
    [InlineData("reset-outputs")]
    [InlineData("select-row")]
    public async Task A_gesture_started_before_flush_cannot_change_the_replacement_settings(string gesture)
    {
        var plugin = new DbcReadPlugin(); var serializer = plugin.CreateSerializer();
        var original = new DbcReadSetting { Document = new() { Text = Catalog }, UseMessageList = true,
            Messages = [new() { MessageId = 1 }, new() { MessageId = 2 }] };
        var step = new Step(); step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion; step.StepSetting.Setting = serializer.Serialize(original);
        var model = new DbcEditorViewModel(plugin);
        try
        {
            model.Attach(step); await model.LoadTask;
            model.BeginMessageSelection(); var panel = model.BatchSelection!;
            panel.Rows.Single(r => r.Choice.Message.Id == 4).Selected = true;
            var oldSecond = model.ConfiguredMessages[1]; var toAdd = model.Messages[3];
            model.ConnectionName = "\"PendingLocal\"";
            var replacement = (DbcReadSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);
            replacement.ConnectionName = "\"Assistant\"";
            replacement.Messages[1].SignalOutputs = [new() { Name = "Value", TargetVariable = "Locals.AssistantTarget" }];
            replacement.Messages.Add(new() { MessageId = 8 });
            var authoritative = serializer.Serialize(replacement); step.StepSetting.Setting = authoritative.ToArray();

            switch (gesture)
            {
                case "apply": model.ApplyMessageSelection(panel); break;
                case "add": Assert.False(model.AddMessages([toAdd])); break;
                case "remove": model.RemoveMessages([oldSecond]); break;
                case "list-mode": model.UseMessageList = false; break;
                case "reset-outputs": model.ResetReadSignalSettings(); break;
                case "select-row": model.SelectedConfiguredMessage = oldSecond; break;
            }

            await model.LoadTask; model.Flush();
            Assert.Equal(authoritative, step.StepSetting.Setting);
            Assert.Equal("\"Assistant\"", model.ConnectionName);
            Assert.True(model.UseMessageList); Assert.Equal(new uint[] { 1, 2, 8 }, model.ConfiguredMessages.Select(r => r.Item.MessageId));
            Assert.Equal("Locals.AssistantTarget", model.ConfiguredMessages[1].Item.SignalOutputs[0].TargetVariable);
            Assert.Equal(1u, model.SelectedConfiguredMessage!.Item.MessageId);
        }
        finally { model.Deactivate(); }
    }
}
