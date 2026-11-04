using CAN.Dbc;
using CAN.Models;
using CAN.UI.Dbc;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcAllSignalsTests
{
    private static DbcDocumentSetting Document() => new() { Name = "all-signals.dbc", Text = """
        VERSION "all-signals"
        BO_ 291 First: 8 ECU
         SG_ Shared : 0|8@1+ (1,0) [0|255] "V" ECU
         SG_ FirstOnly : 8|8@1+ (1,0) [0|255] "V" ECU
        BO_ 292 Second: 8 ECU
         SG_ Shared : 16|8@1+ (2,1) [0|511] "A" ECU
         SG_ SecondOnly : 24|16@1+ (1,0) [0|65535] "A" ECU
         SG_ Counter : 56|8@1+ (1,0) [0|255] "" ECU
        BO_ 2147483939 Extended: 8 ECU
         SG_ Shared : 32|8@1+ (1,0) [0|255] "rpm" ECU
        """ };

    private static Step CreateStep(IStepPlugin plugin)
    {
        var setting = (DbcCanSetting)plugin.CreateSerializer().CreateDefault();
        setting.Document = Document(); setting.UseMessageList = true;
        setting.Messages = [new() { MessageId = 291, Signals = [new() { Name = "Shared", Value = "Locals.First" }],
            SignalOutputs = [new() { Name = "Shared", TargetVariable = "Locals.First" }] },
            new() { MessageId = 292, Signals = [new() { Name = "Shared", Value = "Locals.Second" }],
            SignalOutputs = [new() { Name = "Shared", TargetVariable = "Locals.Second" }],
            Protection = new() { RcEnabled = true, RcStartByte = 7, RcEndByte = 7 } }];
        var step = new Step(); step.StepSetting.Setting = plugin.CreateSerializer().Serialize(setting);
        step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion; return step;
    }

    [WpfFact]
    public async Task All_configured_messages_contribute_signals_after_applying_selection()
    {
        var plugin = new DbcWritePlugin(); var model = new DbcEditorViewModel(plugin); model.Attach(CreateStep(plugin)); await model.LoadTask;
        Assert.Equal(5, model.SignalRows.Count);
        model.BeginMessageSelection(); var selection = model.BatchSelection!;
        selection.Rows.Single(row => row.Choice.Message.Extended).Selected = true;
        model.ApplyMessageSelection(selection);
        Assert.Equal(6, model.SignalRows.Count);
        Assert.Equal(new[] { "Shared", "FirstOnly", "Shared", "SecondOnly", "Counter", "Shared" }, model.SignalRows.Select(row => row.Name));
        model.SelectedConfiguredMessage = model.ConfiguredMessages[1];
        Assert.Equal(6, model.SignalRows.Count);
        model.RemoveMessages([model.ConfiguredMessages[0]]);
        Assert.Equal(4, model.SignalRows.Count);
        model.Deactivate();
    }

    [WpfFact]
    public async Task Same_named_signals_keep_separate_values_outputs_and_protection()
    {
        var plugin = new DbcReadPlugin(); var step = CreateStep(plugin); var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        var shared = model.SignalRows.Where(row => row.Name == "Shared").ToArray();
        Assert.Equal(2, shared.Length);
        Assert.Equal("Locals.First", shared[0].OutputVariable); Assert.Equal("Locals.Second", shared[1].OutputVariable);
        shared[1].OutputVariable = "Locals.NewSecond";
        Assert.Equal("Locals.First", shared[0].OutputVariable);
        Assert.True(model.SignalRows.Single(row => row.Name == "Counter").IsAutomatic);
        model.Flush();
        var saved = (DbcReadSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal("Locals.First", saved.Messages[0].SignalOutputs[0].TargetVariable);
        Assert.Equal("Locals.NewSecond", saved.Messages[1].SignalOutputs[0].TargetVariable);
        model.Deactivate();
    }

    [WpfTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task Every_editor_preserves_all_groups_across_protection_navigation_reload_and_removal(int kind)
    {
        var plugin = Plugin(kind); var step = CreateStep(plugin); var model = new DbcEditorViewModel(plugin);
        var original = step.StepSetting.Setting.ToArray(); model.Attach(step); await model.LoadTask; model.Flush();
        Assert.Equal(original, step.StepSetting.Setting); Assert.Equal(5, model.SignalRows.Count);
        var groups = model.SignalView.Groups!.Cast<CollectionViewGroup>().ToArray();
        Assert.Equal(new[] { 2, 3 }, groups.Select(group => group.ItemCount)); Assert.Contains("5 个信号", model.SignalSummary);
        var firstRows = model.SignalRows.ToArray(); model.SelectedConfiguredMessage = model.ConfiguredMessages[1];
        Assert.Equal(firstRows, model.SignalRows); model.SelectedConfiguredMessage = null;
        Assert.Equal(firstRows, model.SignalRows); Assert.Null(model.Protection);
        model.ConfiguredMessages[1].Enabled = false;
        Assert.Equal(5, model.SignalRows.Count); Assert.Contains("已禁用", model.SignalRows.Last().MessageGroup);
        model.BeginMessageSelection(); model.BatchSelection!.Rows.Single(row => row.Choice.Message.Extended).Selected = true;
        model.ApplyMessageSelection(model.BatchSelection); Assert.Equal(6, model.SignalRows.Count);
        Assert.Equal(3, model.SignalView.Groups!.Count);
        Assert.Equal(2, model.SignalRows.Count(row => row.MessageId == 291 && !row.IsExtended));
        Assert.Single(model.SignalRows, row => row.MessageId == 291 && row.IsExtended);
        model.Flush(); model.Attach(step); await model.LoadTask;
        Assert.Equal(6, model.SignalRows.Count); Assert.Equal(3, model.SignalView.Groups!.Count);
        model.BeginMessageSelection(); model.BatchSelection!.SelectVisible(false); model.ApplyMessageSelection(model.BatchSelection);
        Assert.Empty(model.SignalRows); Assert.Empty(model.SignalView.Groups!); Assert.Contains("0 个信号", model.SignalSummary);
        model.Deactivate();
    }

    [WpfTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Edits_in_non_active_group_register_and_save_in_the_correct_owner(bool read)
    {
        IStepPlugin plugin = read ? new DbcReadPlugin() : new DbcWritePlugin();
        var step = CreateStep(plugin); var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        Assert.Equal(291u, model.SelectedConfiguredMessage!.Item.MessageId);
        var second = model.SignalRows.Single(row => row.Name == "SecondOnly");
        if (read) { second.OutputVariable = "Locals.SecondNew"; second.OutputValueKind = 1; }
        else { second.Selected = true; second.Value = "Locals.SecondNew"; second.ValueKind = 1; }
        model.Flush();
        var saved = (DbcCanSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Single(read ? saved.Messages[0].SignalOutputs.Select(row => row.Name) : saved.Messages[0].Signals.Select(row => row.Name));
        Assert.Equal("Locals.SecondNew", read ? saved.Messages[1].SignalOutputs.Single(row => row.Name == "SecondOnly").TargetVariable
            : saved.Messages[1].Signals.Single(row => row.Name == "SecondOnly").Value);
        Assert.Equal(DbcValueKind.Raw, read ? saved.Messages[1].SignalOutputs.Single(row => row.Name == "SecondOnly").ValueKind
            : saved.Messages[1].Signals.Single(row => row.Name == "SecondOnly").ValueKind);
        model.Attach(step); await model.LoadTask;
        Assert.Equal("Locals.SecondNew", read ? model.SignalRows.Single(row => row.Name == "SecondOnly").OutputVariable
            : model.SignalRows.Single(row => row.Name == "SecondOnly").Value);
        model.Deactivate();
    }

    [WpfFact]
    public async Task Protection_changes_refresh_their_group_and_removed_or_unloaded_rows_cannot_publish()
    {
        var plugin = new DbcWritePlugin(); var step = CreateStep(plugin); var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        var counter = model.SignalRows.Single(row => row.Name == "Counter"); int refreshes = 0;
        counter.PropertyChanged += (_, _) => refreshes++;
        var second = model.ConfiguredMessages[1].Item; second.Protection.RcEnabled = false;
        Assert.False(counter.IsAutomatic); Assert.True(refreshes > 0); Assert.False(model.SignalRows[0].IsAutomatic);
        second.Protection = new() { RcEnabled = true, RcStartByte = 2, RcEndByte = 2 };
        Assert.True(model.SignalRows.Single(row => row.MessageId == 292 && row.Name == "Shared").IsAutomatic);
        Assert.False(model.SignalRows.Single(row => row.MessageId == 291 && row.Name == "Shared").IsAutomatic);
        var old = model.SignalRows.Single(row => row.Name == "SecondOnly");
        model.RemoveMessages([model.ConfiguredMessages[1]]); model.Flush(); var bytes = step.StepSetting.Setting.ToArray();
        old.Selected = true; old.Value = "123"; second.Protection.RcInitialValue = 8; model.Flush();
        Assert.Equal(bytes, step.StepSetting.Setting);
        var current = model.SignalRows[0]; var currentProtection = model.Protection!;
        model.Deactivate(); current.Value = "456"; currentProtection.RcInitialValue = 12; model.Flush();
        Assert.Equal(bytes, step.StepSetting.Setting);
    }

    [WpfFact]
    public async Task Unknown_saved_ID_is_reported_without_hiding_other_valid_groups()
    {
        var plugin = new DbcReadPlugin(); var step = CreateStep(plugin); var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        model.ConfiguredMessages[1].Item.MessageId = 0x777;
        Assert.Equal(2, model.SignalRows.Count); Assert.Contains("1 条未匹配", model.SignalSummary);
        Assert.Contains("未定义", model.LoadError); Assert.Equal(2, model.ConfiguredMessages.Count);
        model.Deactivate();
    }

    [WpfFact]
    public async Task Grouped_signal_projection_publishes_one_complete_reset_without_deferred_current_access()
    {
        var plugin = new DbcWritePlugin(); var model = new DbcEditorViewModel(plugin); model.Attach(CreateStep(plugin)); await model.LoadTask;
        Assert.Empty(model.LoadError); Assert.Equal(5, model.SignalRows.Count);
        int resets = 0;
        void WhenRebuilt(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs change)
        {
            Assert.Equal(System.Collections.Specialized.NotifyCollectionChangedAction.Reset, change.Action);
            resets++;
            Assert.Equal(5, model.SignalRows.Count);
            Assert.Equal(2, model.SignalView.Groups!.Count);
            _ = model.SignalView.CurrentPosition; // WPF consumers may read currency during Reset.
        }
        model.SignalRows.CollectionChanged += WhenRebuilt;
        model.ConfiguredMessages[1].Enabled = false;
        Assert.Equal(1, resets); Assert.Contains("已禁用", model.SignalRows.Last().MessageGroup);
        model.SignalRows.CollectionChanged -= WhenRebuilt;
        model.Deactivate();
        Assert.Empty(model.SignalRows);
        model.Activate(); await model.LoadTask;
        Assert.Equal(5, model.SignalRows.Count); Assert.Contains("已禁用", model.SignalRows.Last().MessageGroup);
        model.Deactivate();
    }

    [WpfFact]
    public async Task Same_step_refresh_invalidates_old_group_rows_without_losing_flushed_edits()
    {
        var plugin = new DbcWritePlugin(); var step = CreateStep(plugin); var model = new DbcEditorViewModel(plugin);
        model.Attach(step); await model.LoadTask;
        var old = model.SignalRows.Single(row => row.MessageId == 292 && row.Name == "Shared");
        old.Value = "Locals.PendingSecond";
        model.Attach(step); await model.LoadTask; var savedBytes = step.StepSetting.Setting.ToArray();
        Assert.Equal("Locals.PendingSecond", model.SignalRows.Single(row => row.MessageId == 292 && row.Name == "Shared").Value);
        old.Value = "Locals.Stale"; old.Selected = false; model.Flush();
        Assert.Equal(savedBytes, step.StepSetting.Setting);
        Assert.Equal("Locals.First", model.SignalRows.Single(row => row.MessageId == 291 && row.Name == "Shared").Value);
        Assert.Equal(5, model.SignalRows.Count); model.Deactivate();
    }

    private static IStepPlugin Plugin(int kind) => kind switch
    {
        0 => new DbcReadPlugin(), 1 => new DbcWritePlugin(), 2 => new DbcCyclicWritePlugin(),
        3 => new DbcCapturePlugin(), _ => new DbcReadBackgroundStartPlugin()
    };

    internal static void VerifyActualPages()
    {
        var dispatcher = Dispatcher.CurrentDispatcher; var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            var loading = PrepareViewsAsync(); var frame = new DispatcherFrame(); bool timeout = false;
            var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(12) };
            timer.Tick += (_, _) => { timeout = true; frame.Continue = false; };
            _ = loading.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            timer.Start(); try { if (!loading.IsCompleted) Dispatcher.PushFrame(frame); } finally { timer.Stop(); }
            Assert.False(timeout, "DBC all-signal models exceeded preload deadline");
            foreach (var (plugin, step, model) in loading.GetAwaiter().GetResult()) VerifyView(plugin, step, model);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private static async Task<List<(IStepPlugin, Step, DbcEditorViewModel)>> PrepareViewsAsync()
    {
        var result = new List<(IStepPlugin, Step, DbcEditorViewModel)>();
        for (int kind = 0; kind < 5; kind++)
        {
            var plugin = Plugin(kind); var step = CreateStep(plugin); var model = new DbcEditorViewModel(plugin);
            model.Attach(step); await model.LoadTask; result.Add((plugin, step, model));
        }
        return result;
    }

    private static void VerifyView(IStepPlugin plugin, Step step, DbcEditorViewModel model)
    {
        var view = DbcStageQueue.Create(model); Layout(view);
        var grid = Assert.IsType<DataGrid>(view.FindName("SignalsGrid"));
        var protectionSelector = Assert.IsType<ComboBox>(view.FindName("ProtectionMessageSelector"));
        var selector = Assert.IsType<ComboBox>(view.FindName("ConfiguredMessageSelector"));
        Assert.Equal(5, grid.Items.Count); Assert.Equal(2, grid.Items.Groups!.Count);
        Assert.Contains("5 个信号", Assert.IsType<TextBlock>(view.FindName("SignalSummaryText")).Text);
        selector.SelectedIndex = 1; Layout(view); Assert.Equal(5, grid.Items.Count);
        protectionSelector.SelectedIndex = 0; Layout(view); Assert.Equal(5, grid.Items.Count);
        var target = model.SignalRows.Single(row => row.Name == "SecondOnly");
        if (model.IsWrite) target.Selected = true;
        var column = grid.Columns.Single(item => item.Header?.ToString() == (model.IsWrite ? "发送值表达式" : "输出变量（留空不输出）"));
        grid.ScrollIntoView(target, column); Layout(view);
        var cell = column.GetCellContent(target); Assert.NotNull(cell);
        if (model.IsWrite)
        {
            var input = DbcRenderTests.EditValue(grid, target, column, view);
            input.GetType().GetProperty("ScriptText")!.SetValue(input, "Locals.FromSecondGroup");
        }
        else DbcRenderTests.EditOutput(grid, target, column, view).Text = "Locals.FromSecondGroup";
        model.Flush(); var saved = (DbcCanSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal("Locals.FromSecondGroup", model.IsWrite ? saved.Messages[1].Signals.Single(row => row.Name == "SecondOnly").Value
            : saved.Messages[1].SignalOutputs.Single(row => row.Name == "SecondOnly").TargetVariable);
        Assert.Equal("Locals.First", model.IsWrite ? saved.Messages[0].Signals[0].Value : saved.Messages[0].SignalOutputs[0].TargetVariable);
        Click(view, "BatchAddButton"); Layout(view);
        Assert.Same(model.BatchSelection, Assert.IsType<Button>(view.FindName("BatchConfirmButton")).DataContext);
        model.BatchSelection!.Rows.Single(row => row.Choice.Message.Extended).Selected = true;
        Click(view, "BatchConfirmButton"); Layout(view); Assert.Equal(6, grid.Items.Count); Assert.Equal(3, grid.Items.Groups!.Count);
        if (model.IsWrite && !model.IsCyclic) SaveImage(view);
        Click(view, "BatchAddButton"); Layout(view);
        Assert.Same(model.BatchSelection, Assert.IsType<Button>(view.FindName("BatchConfirmButton")).DataContext);
        model.BatchSelection!.Rows.Single(row => row.Choice.Message.Id == 292).Selected = false;
        Click(view, "BatchConfirmButton"); Layout(view); Assert.Equal(3, grid.Items.Count); Assert.Equal(2, grid.Items.Groups!.Count);
        Assert.DoesNotContain(grid.Items.Cast<DbcSignalRow>(), row => row.MessageId == 292);
        model.Flush(); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        model.ConnectionName = "\"ReenteredCAN\""; model.Flush();
        var reentered = (DbcCanSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal("\"ReenteredCAN\"", reentered.ConnectionName);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static void Click(DbcEditorView view, string name) => Assert.IsType<Button>(view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Layout(FrameworkElement view) { DbcStageQueue.LayoutAndFinish(view, new Size(1500, 1200)); }
    private static void SaveImage(FrameworkElement view)
    {
        string? directory = Environment.GetEnvironmentVariable("CAN_DBC_ALL_SIGNALS_SCREENSHOTS"); if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory); Layout(view);
        var image = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(Path.Combine(directory, "dbc-all-id-signals.png")); encoder.Save(output);
    }
}
