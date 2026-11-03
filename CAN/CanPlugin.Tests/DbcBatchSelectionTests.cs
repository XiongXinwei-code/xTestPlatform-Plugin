using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CAN.Dbc;
using CAN.Models;
using CAN.UI.Dbc;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcBatchSelectionTests
{
    private static DbcDocumentSetting Document(int count = 140) => new() { Name = "batch.dbc", Text =
        "VERSION \"batch\"\n" + string.Join("\n", Enumerable.Range(1, count).Select(id =>
            $"BO_ {id} Message{id}: 8 ECU\n SG_ Value : 0|8@1+ (1,0) [0|255] \"\" Vector__XXX")) +
        "\nBO_ 2147483649 ExtendedOne: 8 ECU\n SG_ Wide : 0|16@1+ (1,0) [0|65535] \"\" Vector__XXX\n" };
    private static Step Step(IStepPlugin plugin, DbcCanSetting setting)
    { var step = new Step(); step.StepSetting.Setting = plugin.CreateSerializer().Serialize(setting); step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion; return step; }
    private static DbcCanSetting Read(IStepPlugin plugin, Step step) => (DbcCanSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

    [Fact]
    public void Selecting_many_rows_publishes_one_consistent_summary_and_retains_hidden_selections()
    {
        var choices = DbcDatabase.Parse(Document(1000).Text).Messages.Select(m => new DbcMessageChoice(m));
        var selection = new DbcBatchMessageSelection(choices, new HashSet<(uint, bool)> { (1, false) });
        int notifications = 0; selection.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(selection.Summary)) notifications++; };
        selection.SelectVisible(true);
        Assert.Equal(1, notifications); Assert.Equal(1000, selection.Rows.Count(row => row.Selected)); Assert.Contains("已选择 1000 条", selection.Summary);
        notifications = 0; selection.SelectVisible(true);
        Assert.Equal(1, notifications); Assert.Contains("已选择 1000 条", selection.Summary);
        selection.Filter = "ExtendedOne"; notifications = 0; selection.SelectVisible(false);
        Assert.Equal(1, notifications); Assert.Equal(999, selection.Rows.Count(row => row.Selected)); Assert.Contains("已选择 999 条", selection.Summary);
        selection.Filter = ""; notifications = 0; selection.SelectVisible(false);
        Assert.Equal(1, notifications); Assert.DoesNotContain(selection.Rows, row => row.Selected); Assert.Contains("已选择 0 条", selection.Summary);
    }

    [WpfFact]
    public async Task Batch_add_deduplicates_by_frame_identity_and_preserves_existing_signal_protection_and_period()
    {
        IStepPlugin[] plugins = [new DbcReadPlugin(), new DbcWritePlugin(), new DbcCyclicWritePlugin(), new DbcCapturePlugin(), new DbcReadBackgroundStartPlugin()];
        foreach (var plugin in plugins)
        {
            var setting = (DbcCanSetting)plugin.CreateSerializer().CreateDefault(); setting.Document = Document(); setting.UseMessageList = true;
            setting.Messages.Add(new() { MessageId = 1, CycleTimeMs = 72, UseDbcCycleTime = false,
                Signals = [new() { Name = "Value", Value = "Locals.Custom", Selected = true }],
                SignalOutputs = [new() { Name = "Value", TargetVariable = "Locals.Value", Selected = false }],
                Protection = new() { RcEnabled = true, RcStartByte = 7, RcEndByte = 7, RcInitialValue = 4 } });
            var step = Step(plugin, setting); var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
            var source = model.ConfiguredMessages[0].Item;
            var choices = model.Messages.Take(100).Concat(model.Messages.Where(m => m.Message.Extended)).ToArray();
            Assert.True(model.AddMessages(choices.Concat(choices)));
            Assert.Equal(101, model.ConfiguredMessages.Count); Assert.Same(source, model.ConfiguredMessages[0].Item);
            Assert.Equal(4u, source.Protection.RcInitialValue); Assert.Equal("Locals.Custom", source.Signals[0].Value);
            Assert.Equal("Locals.Value", source.SignalOutputs[0].TargetVariable); Assert.False(source.SignalOutputs[0].Selected);
            Assert.Equal(72, source.CycleTimeMs); Assert.False(source.UseDbcCycleTime);
            Assert.Equal(2, model.ConfiguredMessages.Count(row => row.Item.MessageId == 1));
            Assert.All(model.ConfiguredMessages.Skip(1), row => { Assert.False(row.Item.Protection.RcEnabled); Assert.True(row.Item.UseDbcCycleTime); });
            model.Flush(); Assert.Equal(101, Read(plugin, step).Messages.Count); model.Deactivate();
        }
    }

    [WpfFact]
    public async Task Search_selection_survives_filtering_and_already_configured_rows_cannot_be_reset()
    {
        var plugin = new DbcWritePlugin(); var step = Step(plugin, new DbcWriteSetting { Document = Document(), UseMessageList = true, Messages = [new() { MessageId = 1 }] });
        var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask; model.BeginBatchSelection();
        var selection = Assert.IsType<DbcBatchMessageSelection>(model.BatchSelection);
        Assert.False(selection.Rows.Single(row => row.Choice.Message.Id == 1 && !row.Choice.Message.Extended).CanSelect);
        selection.Filter = "Message12"; selection.SelectVisible(true);
        Assert.Equal(11, selection.Rows.Count(row => row.Selected));
        Assert.Contains("已选择 11 条", selection.Summary);
        selection.Filter = "ExtendedOne"; selection.SelectVisible(true); Assert.Equal(12, selection.Rows.Count(row => row.Selected));
        selection.Filter = "0x1 ["; selection.SelectVisible(true); Assert.Equal(12, selection.Rows.Count(row => row.Selected));
        Assert.Contains("已选择 12 条", selection.Summary);
        model.AddBatchSelection(selection); Assert.False(model.HasBatchSelection);
        model.Flush(); Assert.Equal(13, Read(plugin, step).Messages.Count); model.Deactivate();
    }

    [WpfFact]
    public async Task Exceeding_limit_is_atomic_and_batch_can_be_reduced_then_retried()
    {
        var plugin = new DbcReadPlugin(); var setting = new DbcReadSetting { Document = Document(), UseMessageList = true,
            Messages = new(Enumerable.Range(1, 127).Select(i => new DbcMessageItem { MessageId = (uint)i })) };
        var step = Step(plugin, setting); var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        var before = step.StepSetting.Setting.ToArray();
        model.BeginBatchSelection(); var selection = model.BatchSelection!;
        selection.Rows.Single(row => row.Choice.Message.Id == 128).Selected = true;
        selection.Rows.Single(row => row.Choice.Message.Id == 129).Selected = true;
        model.AddBatchSelection(selection); model.Flush();
        Assert.Contains("128", model.LoadError); Assert.Equal(before, step.StepSetting.Setting); Assert.Equal(127, model.ConfiguredMessages.Count);
        selection.Rows.Single(row => row.Choice.Message.Id == 129).Selected = false;
        model.AddBatchSelection(selection); model.Flush(); Assert.Equal(128, Read(plugin, step).Messages.Count); model.Deactivate();
    }

    [WpfTheory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task Batch_mutation_rebinds_after_same_step_refresh_and_aborts_after_step_switch(bool remove, bool switchStep)
    {
        var plugin = new DbcWritePlugin(); var initial = new DbcWriteSetting { Document = Document(), UseMessageList = true,
            Messages = [new() { MessageId = 1 }, new() { MessageId = 2 }, new() { MessageId = 3 }] };
        var step = Step(plugin, initial); var other = Step(plugin, new DbcWriteSetting { Document = Document(), UseMessageList = true });
        var unchanged = other.StepSetting.Setting.ToArray(); var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        var rows = model.ConfiguredMessages.Where(row => row.Item.MessageId != 2).ToArray();
        var choices = model.Messages.Where(row => row.Message.Id is 4 or 5).ToArray();
        QueueRefresh(model, switchStep ? other : step);
        if (remove) model.RemoveMessages(rows); else model.AddMessages(choices);
        await model.LoadTask; model.Flush();
        Assert.Equal(switchStep ? 3 : remove ? 1 : 5, Read(plugin, step).Messages.Count);
        if (switchStep) Assert.Equal(unchanged, other.StepSetting.Setting);
        else if (remove) Assert.Equal(2u, Assert.Single(Read(plugin, step).Messages).MessageId);
        model.Deactivate();
    }

    [WpfFact]
    public async Task Stale_panel_and_old_selected_rows_cannot_modify_refreshed_or_unloaded_editor()
    {
        var plugin = new DbcReadPlugin(); var step = Step(plugin, new DbcReadSetting { Document = Document(), UseMessageList = true, Messages = [new() { MessageId = 1 }] });
        var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask; model.BeginBatchSelection();
        var old = model.BatchSelection!; old.SelectVisible(true); var rows = model.ConfiguredMessages.ToArray();
        model.Attach(step); await model.LoadTask; var before = step.StepSetting.Setting.ToArray();
        model.AddBatchSelection(old); model.RemoveMessages(rows); model.Flush(); Assert.Equal(before, step.StepSetting.Setting);
        model.BeginBatchSelection(); var closed = model.BatchSelection!; closed.Rows[1].Selected = true;
        var current = model.ConfiguredMessages.ToArray(); model.Deactivate();
        model.AddBatchSelection(closed); model.RemoveMessages(current); model.Flush(); Assert.Equal(before, step.StepSetting.Setting);
    }

    [WpfFact]
    public async Task Single_ID_migration_is_not_partially_applied_when_batch_exceeds_capacity()
    {
        var plugin = new DbcWritePlugin(); var step = Step(plugin, new DbcWriteSetting { UseMessageList = false, Document = Document(), MessageId = 1,
            Signals = [new() { Name = "Value", Selected = true, Value = "Locals.Value" }] });
        var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        var before = step.StepSetting.Setting.ToArray();
        Assert.False(model.AddMessages(model.Messages.Where(m => !m.Message.Extended && m.Message.Id > 1).Take(128)));
        model.Flush(); Assert.Equal(before, step.StepSetting.Setting); Assert.False(model.UseMessageList); Assert.Empty(model.ConfiguredMessages);
        model.Deactivate();
    }

    [WpfTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Document_replacement_during_Flush_prevents_old_batch_mutation(bool remove)
    {
        var plugin = new DbcWritePlugin(); var setting = new DbcWriteSetting { Document = Document(), UseMessageList = true, Messages = [new() { MessageId = 1 }] };
        var step = Step(plugin, setting); var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        var oldRows = model.ConfiguredMessages.ToArray(); var choices = model.Messages.Where(m => m.Message.Id == 2).ToArray();
        var replacement = new DbcWriteSetting { Document = Document(2), UseMessageList = true, Messages = [new() { MessageId = 2 }] };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var scheduler = typeof(DbcEditorViewModel).GetField("_save", flags)!.GetValue(model)!;
        Action replace = () => { step.StepSetting.Setting = plugin.CreateSerializer().Serialize(replacement); model.Attach(step); };
        scheduler.GetType().GetMethod("Queue")!.Invoke(scheduler, [replace]);
        if (remove) model.RemoveMessages(oldRows); else Assert.False(model.AddMessages(choices));
        await model.LoadTask; model.Flush(); Assert.Equal(2u, Assert.Single(Read(plugin, step).Messages).MessageId); model.Deactivate();
    }

    private static void QueueRefresh(DbcEditorViewModel model, Step refreshed)
    {
        model.ExecuteCommand = (_, apply) => { apply(); model.Attach(refreshed); };
        model.TimeoutMs++;
    }

    internal static void VerifyActualPages()
    {
        WriteProgress("DBC suite enter");
        var dispatcher = Dispatcher.CurrentDispatcher; var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            // Preload DBC models before constructing any Syncfusion control. Pumping
            // unrelated SDK license notifications while views exist enters a modal
            // Window.ShowDialog in the unlicensed software-only test host.
            var task = PreparePagesAsync(); WriteProgress("DBC model preload task created"); var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(12) };
            bool timedOut = false; timer.Tick += (_, _) => { WriteProgress("DBC 12-second dispatcher timeout"); timedOut = true; frame.Continue = false; };
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            timer.Start(); try { if (!task.IsCompleted) Dispatcher.PushFrame(frame); } finally { timer.Stop(); }
            WriteProgress("DBC preload frame exited"); Assert.False(timedOut, "DBC batch model preload exceeded 12 seconds");
            VerifyPreparedPages(task.GetAwaiter().GetResult());
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); WriteProgress("DBC suite exit"); }
    }
    private static async Task<List<(IStepPlugin Plugin, Step Step, DbcEditorViewModel Model)>> PreparePagesAsync()
    {
        IStepPlugin[] plugins = [new DbcReadPlugin(), new DbcWritePlugin(), new DbcCyclicWritePlugin(), new DbcCapturePlugin(), new DbcReadBackgroundStartPlugin()];
        var prepared = new List<(IStepPlugin, Step, DbcEditorViewModel)>();
        foreach (var plugin in plugins)
        {
            WriteProgress(plugin.StepTypeId + " preload model");
            var setting = (DbcCanSetting)plugin.CreateSerializer().CreateDefault(); setting.Document = Document(); setting.UseMessageList = true;
            var step = Step(plugin, setting); var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
            prepared.Add((plugin, step, model));
        }
        return prepared;
    }
    private static void VerifyPreparedPages(List<(IStepPlugin Plugin, Step Step, DbcEditorViewModel Model)> prepared)
    {
        foreach (var (plugin, step, model) in prepared)
        {
            WriteProgress(plugin.StepTypeId + " construct preloaded view"); var view = DbcStageQueue.Create(model);
            WriteProgress(plugin.StepTypeId + " initial layout"); Layout(view); WriteProgress(plugin.StepTypeId + " open batch"); Click(view, "BatchAddButton"); Layout(view);
            Assert.Equal(Visibility.Visible, ((Border)view.FindName("BatchSelectionPanel")).Visibility);
            ((TextBox)view.FindName("BatchFilterBox")).Text = "Message12";
            WriteProgress(plugin.StepTypeId + " select filtered");
            Click(view, "BatchSelectVisibleButton"); Assert.Equal(11, view.ViewModel.BatchSelection!.Rows.Count(row => row.Selected));
            if (plugin is DbcWritePlugin) { WriteProgress(plugin.StepTypeId + " render screenshot"); SaveImage(view, "DbcBatchSelection.png"); }
            WriteProgress(plugin.StepTypeId + " confirm batch"); Click(view, "BatchConfirmButton"); WriteProgress(plugin.StepTypeId + " flush added"); view.ViewModel.Flush(); Assert.Equal(11, Read(plugin, step).Messages.Count);
            var grid = (DataGrid)view.FindName("MessagesGrid"); Assert.Equal(DataGridSelectionMode.Extended, grid.SelectionMode); Assert.Equal(DataGridSelectionUnit.FullRow, grid.SelectionUnit);
            WriteProgress(plugin.StepTypeId + " select rows for removal"); grid.SelectedItems.Clear(); grid.SelectedItems.Add(view.ViewModel.ConfiguredMessages[0]); grid.SelectedItems.Add(view.ViewModel.ConfiguredMessages[2]);
            WriteProgress(plugin.StepTypeId + " remove selected"); Click(view, "RemoveMessagesButton"); view.ViewModel.Flush(); Assert.Equal(9, Read(plugin, step).Messages.Count);
            // Attach synchronously restores saved message rows. Async parse/re-entry
            // and stale callbacks are covered by the separate WpfFact model tests.
            WriteProgress(plugin.StepTypeId + " refresh saved rows"); view.RefreshFromStep(step); Assert.Equal(9, view.ViewModel.ConfiguredMessages.Count);
            WriteProgress(plugin.StepTypeId + " unload");
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            WriteProgress(plugin.StepTypeId + " complete");
        }
    }
    internal static void WriteProgress(string phase)
    {
        var screenshots = Environment.GetEnvironmentVariable("CAN_DBC_STATUS_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(screenshots)) return;
        var directory = Directory.GetParent(screenshots)?.FullName ?? screenshots; Directory.CreateDirectory(directory);
        File.AppendAllText(Path.Combine(directory, "ui-progress.txt"), $"{DateTimeOffset.Now:O} [T{Environment.CurrentManagedThreadId}] {phase}{Environment.NewLine}");
    }
    private static void Click(DbcEditorView view, string name) => ((Button)view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Layout(FrameworkElement view) { DbcStageQueue.LayoutAndFinish(view, new Size(1200, 1000)); }
    private static void SaveImage(FrameworkElement view, string name)
    {
        Layout(view); var directory = Environment.GetEnvironmentVariable("CAN_DBC_STATUS_SCREENSHOTS"); if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory); var image = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var output = File.Create(Path.Combine(directory, name)); encoder.Save(output);
    }
}
