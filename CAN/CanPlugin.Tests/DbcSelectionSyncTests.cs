using CAN.Dbc;
using CAN.Models;
using CAN.UI.Dbc;
using CAN.UI.Views;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcSelectionSyncTests
{
    private static DbcDocumentSetting Document() => new() { Name = "selection.dbc", Text = """
        VERSION "selection"
        BO_ 291 First: 8 ECU
         SG_ FirstValue : 0|8@1+ (1,0) [0|255] "V" ECU
        BO_ 292 Second: 8 ECU
         SG_ SecondValue : 16|16@1+ (2,1) [0|131071] "A" ECU
        BO_ 2147483939 Extended: 8 ECU
         SG_ ExtendedValue : 32|8@1+ (1,0) [0|255] "rpm" ECU
        BA_ "GenSigStartValue" SG_ 291 FirstValue 7;
        BA_ "GenSigStartValue" SG_ 292 SecondValue 11;
        BA_ "GenSigStartValue" SG_ 2147483939 ExtendedValue 13;
        """ };
    private static DbcMessageItem Item(uint id, string signal, string value, uint rc, CanFrameType type = CanFrameType.Standard) => new()
    {
        MessageId = id, FrameType = type, UseDbcCycleTime = false, CycleTimeMs = (int)id,
        Signals = [new() { Name = signal, Selected = true, Value = value }],
        SignalOutputs = [new() { Name = signal, TargetVariable = "Locals." + signal }],
        Protection = new() { RcEnabled = true, RcStartByte = 7, RcEndByte = 7, RcInitialValue = rc }
    };
    private static Step CreateStep(IStepPlugin plugin)
    {
        var setting = (DbcCanSetting)plugin.CreateSerializer().CreateDefault();
        setting.Document = Document(); setting.UseMessageList = true;
        setting.Messages = [Item(291, "FirstValue", "Locals.First", 2), Item(292, "SecondValue", "Locals.Second", 4), Item(291, "ExtendedValue", "Locals.Extended", 6, CanFrameType.Extended)];
        var step = new Step(); step.StepSetting.Setting = plugin.CreateSerializer().Serialize(setting);
        step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion; return step;
    }

    [WpfFact]
    public async Task Multi_ID_choice_selects_configured_row_without_replacing_ID_or_signal_settings()
    {
        var plugin = new DbcWritePlugin(); var step = CreateStep(plugin);
        var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        var first = model.ConfiguredMessages[0].Item;
        model.SelectedMessage = model.Messages.Single(m => m.Message.Id == 292);
        Assert.Equal(291u, first.MessageId); Assert.Equal("Locals.First", Assert.Single(first.Signals).Value);
        Assert.Equal(292u, model.SelectedConfiguredMessage!.Item.MessageId);
        Assert.Equal("SecondValue", CurrentSignal(model).Name);
        Assert.Equal(16, CurrentSignal(model).Signal.StartBit);
        Assert.Equal(11m, CurrentSignal(model).Signal.InitialRawValue);
        Assert.Equal(4u, model.Protection!.RcInitialValue);
        model.Deactivate();
    }

    [WpfFact]
    public async Task Empty_protection_choice_keeps_all_signals_and_selecting_row_restores_matching_protection()
    {
        var plugin = new DbcReadPlugin(); var model = new DbcEditorViewModel(plugin); model.Attach(CreateStep(plugin)); await model.LoadTask;
        model.SelectedMessage = null;
        Assert.Equal(3, model.SignalRows.Count); Assert.Null(model.Protection);
        model.SelectedConfiguredMessage = model.ConfiguredMessages[2];
        Assert.Equal("ExtendedValue", CurrentSignal(model).Name);
        Assert.Equal("Locals.ExtendedValue", CurrentSignal(model).OutputVariable);
        Assert.Equal(6u, model.Protection!.RcInitialValue);
        model.RemoveMessages([model.SelectedConfiguredMessage]);
        Assert.Equal("FirstValue", CurrentSignal(model).Name);
        Assert.Equal(2u, model.Protection!.RcInitialValue);
        model.Deactivate();
    }

    private static IStepPlugin Plugin(int kind) => kind switch
    {
        0 => new DbcReadPlugin(), 1 => new DbcWritePlugin(), 2 => new DbcCyclicWritePlugin(),
        3 => new DbcCapturePlugin(), _ => new DbcReadBackgroundStartPlugin()
    };

    [WpfTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task Selection_applies_additions_and_removals_preserves_retained_settings_and_reloads_distinct_signals(int kind)
    {
        var plugin = Plugin(kind); var step = CreateStep(plugin); var model = new DbcEditorViewModel(plugin);
        model.Attach(step); await model.LoadTask;
        model.RemoveMessages([model.ConfiguredMessages[2]]);
        model.SelectedConfiguredMessage = model.ConfiguredMessages[1]; var retained = model.SelectedConfiguredMessage.Item;
        model.BeginMessageSelection(); var selection = model.BatchSelection!;
        Assert.Equal(2, selection.Rows.Count(row => row.Selected)); Assert.All(selection.Rows, row => Assert.True(row.CanSelect));
        selection.Filter = "First"; Assert.Single(selection.VisibleRows).Selected = false;
        selection.Filter = "Extended"; Assert.Single(selection.VisibleRows).Selected = true;
        model.ApplyMessageSelection(selection);
        Assert.Equal(2, model.ConfiguredMessages.Count); Assert.Same(retained, model.SelectedConfiguredMessage!.Item);
        Assert.Equal("SecondValue", CurrentSignal(model).Name); Assert.Equal(4u, model.Protection!.RcInitialValue);
        Assert.Equal("Locals.Second", Assert.Single(retained.Signals).Value); Assert.Equal("Locals.SecondValue", Assert.Single(retained.SignalOutputs).TargetVariable);
        Assert.Equal(292, retained.CycleTimeMs); Assert.False(retained.UseDbcCycleTime);
        model.SelectedConfiguredMessage = model.ConfiguredMessages.Single(row => row.Item.FrameType == CanFrameType.Extended);
        Assert.Equal("ExtendedValue", CurrentSignal(model).Name); Assert.Equal(32, CurrentSignal(model).Signal.StartBit);
        Assert.Equal(13m, CurrentSignal(model).Signal.InitialRawValue);
        model.Flush(); model.Attach(step); await model.LoadTask;
        Assert.Equal(CanFrameType.Extended, model.SelectedConfiguredMessage!.Item.FrameType);
        Assert.Equal("ExtendedValue", CurrentSignal(model).Name);
        model.BeginMessageSelection(); model.BatchSelection!.SelectVisible(false); model.ApplyMessageSelection(model.BatchSelection);
        Assert.Empty(model.ConfiguredMessages); Assert.Empty(model.SignalRows); Assert.Null(model.SelectedMessage); Assert.Null(model.Protection);
        model.Flush(); model.Attach(step); await model.LoadTask; Assert.Empty(model.SignalRows);
        model.Deactivate();
    }

    [WpfTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Selection_rebinds_same_step_after_save_and_rejects_a_new_step(bool switchStep)
    {
        var plugin = new DbcWritePlugin(); var step = CreateStep(plugin); var other = CreateStep(plugin);
        var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        model.SelectedConfiguredMessage = model.ConfiguredMessages[1]; model.BeginMessageSelection(); var selection = model.BatchSelection!;
        selection.Rows.Single(row => row.Choice.Message.Id == 291 && !row.Choice.Message.Extended).Selected = false;
        model.ExecuteCommand = (_, apply) => { apply(); model.Attach(switchStep ? other : step); };
        model.TimeoutMs++;
        model.ApplyMessageSelection(selection); await model.LoadTask;
        Assert.Equal(switchStep ? 3 : 2, model.ConfiguredMessages.Count);
        Assert.Equal(switchStep ? "FirstValue" : "SecondValue", CurrentSignal(model).Name);
        model.Deactivate();
    }

    [WpfFact]
    public async Task Old_checklist_cannot_modify_reloaded_or_unloaded_editor()
    {
        var plugin = new DbcWritePlugin(); var step = CreateStep(plugin); var model = new DbcEditorViewModel(plugin);
        model.Attach(step); await model.LoadTask; model.BeginMessageSelection(); var old = model.BatchSelection!; old.SelectVisible(false);
        model.Attach(step); await model.LoadTask; model.ApplyMessageSelection(old);
        Assert.Equal(3, model.ConfiguredMessages.Count); Assert.Equal("FirstValue", CurrentSignal(model).Name);
        model.BeginMessageSelection(); var closed = model.BatchSelection!; closed.SelectVisible(false); model.Deactivate();
        var saved = step.StepSetting.Setting.ToArray();
        model.ApplyMessageSelection(closed); Assert.Empty(model.ConfiguredMessages); Assert.Empty(model.SignalRows);
        model.Activate(); await model.LoadTask;
        Assert.Equal(3, model.ConfiguredMessages.Count); Assert.Equal(saved, step.StepSetting.Setting);
        model.Deactivate();
    }

    [WpfFact]
    public async Task Oversized_replacement_is_atomic_and_unmatched_old_ID_requires_explicit_removal()
    {
        var plugin = new DbcWritePlugin(); var setting = new DbcWriteSetting { Document = new() { Name = "limit.dbc", Text =
            "VERSION \"limit\"\n" + string.Join("\n", Enumerable.Range(1, 129).Select(id => $"BO_ {id} Message{id}: 8 ECU\n SG_ Signal{id} : 0|8@1+ (1,0) [0|255] \"\" ECU")) },
            UseMessageList = true, Messages = [Item(1, "Signal1", "Locals.Keep", 8), Item(1000, "Missing", "Locals.Missing", 9)] };
        var step = new Step(); step.StepSetting.Setting = plugin.CreateSerializer().Serialize(setting); step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion;
        var before = step.StepSetting.Setting.ToArray(); var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        model.BeginMessageSelection(); var selection = model.BatchSelection!; selection.SelectVisible(true); model.ApplyMessageSelection(selection); model.Flush();
        Assert.Equal(before, step.StepSetting.Setting); Assert.Equal(2, model.ConfiguredMessages.Count); Assert.Contains("128", model.LoadError);
        selection.SelectVisible(false); model.ApplyMessageSelection(selection);
        Assert.Equal(1000u, Assert.Single(model.ConfiguredMessages).Item.MessageId); Assert.Empty(model.SignalRows);
        model.RemoveMessages(model.ConfiguredMessages.ToArray()); Assert.Empty(model.ConfiguredMessages); Assert.Null(model.Protection); model.Deactivate();
    }

    internal static void VerifyActualPages()
    {
        DbcEditorTestContext.Run(nameof(DbcSelectionSyncTests), TimeSpan.FromSeconds(12), async ui =>
        {
            var prepared = await PrepareViewsAsync(ui);
            foreach (var (plugin, step, model) in prepared) VerifyView(plugin, step, model);
        });
    }

    private static async Task<List<(IStepPlugin, Step, DbcEditorViewModel)>> PrepareViewsAsync(DbcEditorTestContext ui)
    {
        var result = new List<(IStepPlugin, Step, DbcEditorViewModel)>();
        for (int kind = 0; kind < 5; kind++)
        {
            var plugin = Plugin(kind); var step = CreateStep(plugin); var model = ui.CreateModel(plugin);
            model.Attach(step); await model.LoadTask; model.RemoveMessages([model.ConfiguredMessages[2]]); model.Flush();
            result.Add((plugin, step, model));
        }
        return result;
    }
    private static void VerifyView(IStepPlugin plugin, Step step, DbcEditorViewModel model)
    {
        var view = DbcStageQueue.Create(model); Layout(view);
        var tabs = Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(view.Content);
        var selector = (ComboBox)view.FindName("ConfiguredMessageSelector");
        var protectionSelector = (ComboBox)view.FindName("ProtectionMessageSelector");
        var signals = (DataGrid)view.FindName("SignalsGrid"); var messages = (DataGrid)view.FindName("MessagesGrid");
        Assert.Null(view.FindName("ProtectionEditor"));
        selector.SelectedIndex = 1; Layout(view);
        Assert.Equal(2, signals.Items.Count);
        Assert.Equal("SecondValue", CurrentGridSignal(signals, model).Name);
        Assert.Same(model.ConfiguredMessages[1], protectionSelector.SelectedItem);
        if (plugin is DbcWritePlugin) SaveImage(view, "dbc-signals-selected-id.png");
        tabs.SelectedIndex = 1; Layout(view);
        var protection = Assert.IsType<PayloadProtectionEditorView>(view.FindName("ProtectionEditor"));
        Assert.Same(model.Protection, protection.DataContext);
        Assert.Equal(4u, Assert.IsType<CanPayloadProtectionSetting>(protection.DataContext).RcInitialValue);
        uint expectedSecondRc = 4;
        if (model.IsWrite)
        {
            var rcInput = Assert.IsType<TextBox>(((PayloadProtectionEditorView)protection).FindName("RcInitialValueBox"));
            Assert.True(rcInput.IsEnabled); rcInput.Text = "9";
            expectedSecondRc = 9;
            Assert.Equal(expectedSecondRc, model.Protection!.RcInitialValue);
        }
        if (plugin is DbcWritePlugin) SaveImage(view, "dbc-protection-selected-id.png");
        protectionSelector.SelectedIndex = 0; Layout(view);
        Assert.Same(model.ConfiguredMessages[0], selector.SelectedItem);
        Assert.Equal("FirstValue", CurrentGridSignal(signals, model).Name);
        Assert.Equal(2u, Assert.IsType<CanPayloadProtectionSetting>(protection.DataContext).RcInitialValue);
        protectionSelector.SelectedIndex = 1; Layout(view);
        Assert.Equal(expectedSecondRc, Assert.IsType<CanPayloadProtectionSetting>(protection.DataContext).RcInitialValue);
        tabs.SelectedIndex = 0; Layout(view);
        selector.SelectedIndex = 1;
        Click(view, "BatchAddButton"); Layout(view);
        Assert.Equal(2, model.BatchSelection!.Rows.Count(row => row.Selected));
        if (plugin is DbcWritePlugin) SaveImage(view, "dbc-message-selection.png");
        ((TextBox)view.FindName("BatchFilterBox")).Text = "First";
        Assert.Single(model.BatchSelection.VisibleRows).Selected = false;
        ((TextBox)view.FindName("BatchFilterBox")).Text = "Extended";
        Click(view, "BatchSelectVisibleButton"); Click(view, "BatchConfirmButton"); Layout(view);
        Assert.Equal(2, messages.Items.Count); Assert.Equal("SecondValue", CurrentGridSignal(signals, model).Name);
        Assert.Same(model.SelectedConfiguredMessage, selector.SelectedItem); Assert.Same(selector.SelectedItem, protectionSelector.SelectedItem);
        protectionSelector.SelectedIndex = 1; Layout(view);
        var extended = CurrentGridSignal(signals, model); Assert.Equal("ExtendedValue", extended.Name); Assert.Equal(32, extended.Signal.StartBit);
        Assert.Same(model.Protection, protection.DataContext); Assert.Equal(CanFrameType.Extended, model.SelectedConfiguredMessage!.Item.FrameType);
        model.Flush(); var saved = (DbcCanSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal("Locals.Second", saved.Messages[0].Signals[0].Value); Assert.Equal(expectedSecondRc, saved.Messages[0].Protection.RcInitialValue);
        Assert.Equal(0u, saved.Messages[1].Protection.RcInitialValue);
        var savedBytes = step.StepSetting.Setting.ToArray();
        messages.SelectedItems.Clear(); foreach (var row in model.ConfiguredMessages.ToArray()) messages.SelectedItems.Add(row);
        Click(view, "RemoveMessagesButton"); Layout(view);
        Assert.Empty(messages.Items); Assert.Empty(signals.Items); Assert.Null(selector.SelectedItem); Assert.Null(protectionSelector.SelectedItem); Assert.Null(protection.DataContext);
        model.Flush(); view.RefreshFromStep(step); Assert.Empty(model.ConfiguredMessages); Assert.Empty(signals.Items);
        // Restore the saved configuration through the real view. Protection is
        // restored synchronously; DBC parsing/Signals reload is covered above in
        // the asynchronous VM tests without pumping licensing dialogs here.
        step.StepSetting.Setting = savedBytes; view.RefreshFromStep(step); tabs.SelectedIndex = 1; Layout(view);
        Assert.Equal(expectedSecondRc, Assert.IsType<CanPayloadProtectionSetting>(protection.DataContext).RcInitialValue);
        protectionSelector.SelectedIndex = 1; Layout(view);
        Assert.Equal(0u, Assert.IsType<CanPayloadProtectionSetting>(protection.DataContext).RcInitialValue);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }
    private static DbcSignalRow CurrentSignal(DbcEditorViewModel model) => model.SignalRows.Single(row =>
        row.MessageId == model.SelectedConfiguredMessage!.Item.MessageId && row.IsExtended == (model.SelectedConfiguredMessage.Item.FrameType == CanFrameType.Extended));
    private static DbcSignalRow CurrentGridSignal(DataGrid grid, DbcEditorViewModel model) => grid.Items.Cast<DbcSignalRow>().Single(row =>
        row.MessageId == model.SelectedConfiguredMessage!.Item.MessageId && row.IsExtended == (model.SelectedConfiguredMessage.Item.FrameType == CanFrameType.Extended));
    private static void Click(DbcEditorView view, string name) => ((Button)view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Layout(FrameworkElement view) { DbcStageQueue.LayoutAndFinish(view, new Size(1200, 1000)); }
    private static void SaveImage(FrameworkElement view, string name)
    {
        var directory = Environment.GetEnvironmentVariable("CAN_DBC_SELECTION_SCREENSHOTS"); if (string.IsNullOrWhiteSpace(directory)) return;
        Layout(view); Directory.CreateDirectory(directory);
        var image = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(Path.Combine(directory, name)); encoder.Save(output);
    }
}
