using CAN.Dbc;
using CAN.Models;
using CAN.UI.Dbc;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class DbcExplicitSelectionTests
{
    private const string Text = """
        BO_ 0 Zero: 1 ECU
         SG_ ZeroValue : 0|8@1+ (1,0) [0|255] "" ECU
        BO_ 291 Second: 1 ECU
         SG_ SecondValue : 0|8@1+ (1,0) [0|255] "" ECU
        """;
    private static IStepPlugin[] Plugins() => [new DbcReadPlugin(), new DbcWritePlugin(), new DbcCyclicWritePlugin(),
        new DbcCapturePlugin(), new DbcReadBackgroundStartPlugin()];
    private static Step MakeStep(IStepPlugin plugin, object setting)
    {
        var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion;
        return step;
    }
    private static DbcDocumentSetting Document() => new() { Name = "zero.dbc", Text = Text };

    [Fact]
    public void Fresh_DBC_defaults_require_explicit_message_selection()
    {
        foreach (var plugin in Plugins())
        {
            var serializer = plugin.CreateSerializer();
            var setting = Assert.IsAssignableFrom<DbcCanSetting>(serializer.CreateDefault());
            Assert.True(setting.UseMessageList, plugin.StepTypeId);
            Assert.Empty(setting.Messages); Assert.Equal(new Step().StepSetting.SettingVersion, serializer.SettingVersion);
        }
    }

    [Fact]
    public async Task Loaded_catalog_without_selection_is_rejected_before_execution()
    {
        foreach (var plugin in Plugins())
        {
            var setting = (DbcCanSetting)plugin.CreateSerializer().CreateDefault(); setting.Document = Document();
            DbcEditorPluginBase editor = plugin switch
            {
                DbcReadBackgroundStartPlugin => new DbcReadBackgroundStartEditorPlugin(),
                DbcReadPlugin => new DbcReadEditorPlugin(),
                DbcWritePlugin => new DbcWriteEditorPlugin(),
                DbcCyclicWritePlugin => new DbcCyclicWriteEditorPlugin(),
                DbcCapturePlugin => new DbcCaptureEditorPlugin(),
                _ => throw new InvalidOperationException()
            };
            var errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
            Assert.Contains(errors, error => error.Code == "DBC_MESSAGE_COUNT");
        }
    }

    [WpfFact]
    public async Task File_load_and_reload_do_not_choose_first_or_ID_zero_for_any_DBC_step()
    {
        foreach (var plugin in Plugins())
        {
            var serializer = plugin.CreateSerializer(); var setting = (DbcCanSetting)serializer.CreateDefault();
            var step = MakeStep(plugin, setting);
            var model = new DbcEditorViewModel(plugin, (_, _, _) => Task.FromResult(Document()));
            model.Attach(step); await model.LoadFileAsync("unused.dbc"); model.Flush();
            Assert.Equal(2, model.Messages.Count); Assert.Empty(model.ConfiguredMessages); Assert.Empty(model.SignalRows);
            Assert.Null(model.SelectedMessage); Assert.Null(model.MessageToAdd); Assert.False(model.HasActiveMessage);
            model.Attach(step); await model.LoadTask; await model.ReloadEmbeddedAsync();
            Assert.Empty(model.ConfiguredMessages); Assert.Empty(model.SignalRows); Assert.Null(model.SelectedMessage);
            Assert.Empty(((DbcCanSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion)).Messages);
            model.Deactivate();
        }
    }

    [WpfFact]
    public async Task Choosing_single_mode_before_first_import_does_not_implicitly_select_real_ID_zero()
    {
        var plugin = new DbcWritePlugin(); var step = MakeStep(plugin, new DbcWriteSetting());
        var model = new DbcEditorViewModel(plugin, (_, _, _) => Task.FromResult(Document()));
        model.Attach(step); model.UseMessageList = false;
        await model.LoadFileAsync("unused.dbc"); model.Flush();
        Assert.True(model.UseMessageList); Assert.Empty(model.ConfiguredMessages);
        Assert.Null(model.SelectedMessage); Assert.Empty(model.SignalRows); model.Deactivate();
    }

    [WpfFact]
    public async Task Replacing_document_preserves_explicit_single_inputs_and_does_not_substitute_missing_ID()
    {
        var plugin = new DbcWritePlugin();
        var setting = new DbcWriteSetting { UseMessageList = false, Document = Document(), MessageId = 0,
            Signals = [new() { Name = "ZeroValue", Value = "42" }] };
        var replacement = Document(); var step = MakeStep(plugin, setting);
        var model = new DbcEditorViewModel(plugin, (_, _, _) => Task.FromResult(replacement));
        model.Attach(step); await model.LoadTask; await model.LoadFileAsync("same.dbc"); model.Flush();
        Assert.Equal(0u, model.SelectedMessage!.Message.Id); Assert.Equal("42", Assert.Single(model.SignalRows).Value);
        replacement = new() { Name = "other.dbc", Text = "BO_ 7 Other: 1 ECU\n SG_ OtherValue : 0|8@1+ (1,0) [0|255] \"\" ECU" };
        await model.LoadFileAsync("other.dbc"); model.Flush();
        Assert.Null(model.SelectedMessage); Assert.Empty(model.SignalRows); Assert.Contains("未自动替换", model.LoadError);
        var saved = (DbcWriteSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal(0u, saved.MessageId); Assert.Equal("42", Assert.Single(saved.Signals).Value); model.Deactivate();
    }

    [WpfFact]
    public async Task First_snapshot_import_preserves_single_nonzero_ID_and_configured_write_value()
    {
        var plugin = new DbcWritePlugin(); var serializer = plugin.CreateSerializer();
        var initial = new DbcWriteSetting { UseMessageList = false, MessageId = 291,
            Signals = [new() { Name = "SecondValue", Selected = true, Value = "Locals.BeforeImport", ValueKind = DbcValueKind.Raw }] };
        var step = MakeStep(plugin, initial); var model = new DbcEditorViewModel(plugin, (_, _, _) => Task.FromResult(Document()));
        try
        {
            model.Attach(step); await model.LoadFileAsync("unused.dbc"); model.Flush();
            var saved = (DbcWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.False(saved.UseMessageList); Assert.Equal(291u, saved.MessageId);
            var input = Assert.Single(saved.Signals); Assert.True(input.Selected); Assert.Equal(DbcValueKind.Raw, input.ValueKind);
            Assert.Equal("Locals.BeforeImport", input.Value); Assert.Equal(input.Value, Assert.Single(model.SignalRows).Value);
        }
        finally { model.Deactivate(); }
    }

    [WpfTheory]
    [InlineData("mapping")] [InlineData("protection")] [InlineData("extended")] [InlineData("empty")]
    public async Task First_snapshot_import_distinguishes_explicit_zero_from_an_empty_single_ID(string kind)
    {
        var plugin = new DbcReadPlugin(); var serializer = plugin.CreateSerializer();
        var initial = new DbcReadSetting { UseMessageList = false, MessageId = 0 };
        if (kind == "mapping") initial.SignalOutputs.Add(new() { Name = "ZeroValue", TargetVariable = "Locals.ZeroBeforeImport", ValueKind = DbcValueKind.Raw });
        if (kind == "protection") initial.Protection.RcInitialValue = 5;
        if (kind == "extended") initial.FrameType = CanFrameType.Extended;
        var document = Document(); document.Text += "\nBO_ 2147483648 ExtendedZero: 1 ECU\n SG_ ExtendedValue : 0|8@1+ (1,0) [0|255] \"\" ECU";
        var step = MakeStep(plugin, initial); var model = new DbcEditorViewModel(plugin, (_, _, _) => Task.FromResult(document));
        try
        {
            model.Attach(step); await model.LoadFileAsync("unused.dbc"); model.Flush();
            var saved = (DbcReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.Equal(kind == "empty", saved.UseMessageList); Assert.Empty(saved.Messages);
            if (kind == "empty") { Assert.Null(model.SelectedMessage); Assert.Empty(model.SignalRows); }
            else
            {
                Assert.Equal(0u, model.SelectedMessage!.Message.Id); Assert.Equal(kind == "extended", model.SelectedMessage.Message.Extended);
                Assert.Equal(initial.FrameType, saved.FrameType); Assert.Equal(initial.Protection.RcInitialValue, saved.Protection.RcInitialValue);
                if (kind == "mapping")
                {
                    var output = Assert.Single(saved.SignalOutputs); Assert.Equal("Locals.ZeroBeforeImport", output.TargetVariable);
                    Assert.Equal(DbcValueKind.Raw, output.ValueKind); Assert.Equal(output.TargetVariable, Assert.Single(model.SignalRows).OutputVariable);
                }
            }
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Explicit_selection_and_removing_all_persist_without_automatic_replacement()
    {
        var plugin = new DbcWritePlugin(); var serializer = plugin.CreateSerializer();
        var setting = (DbcWriteSetting)serializer.CreateDefault(); setting.Document = Document();
        var step = MakeStep(plugin, setting); var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask;
        Assert.True(model.AddMessages([model.Messages[1]]));
        Assert.Equal(291u, Assert.Single(model.ConfiguredMessages).Item.MessageId);
        Assert.Equal("SecondValue", Assert.Single(model.SignalRows).Name);
        model.Flush(); model.Attach(step); await model.LoadTask;
        Assert.Equal(291u, Assert.Single(model.ConfiguredMessages).Item.MessageId);
        model.RemoveMessages(model.ConfiguredMessages.ToArray()); model.Flush();
        model.Attach(step); await model.LoadTask;
        Assert.Empty(model.ConfiguredMessages); Assert.Empty(model.SignalRows); Assert.Null(model.SelectedMessage);
        model.Deactivate();
    }

    [Fact]
    public void Current_format_round_trip_preserves_explicit_list_and_nested_signal_configuration()
    {
        foreach (var plugin in Plugins())
        {
            var serializer = plugin.CreateSerializer();
            var current = (DbcCanSetting)serializer.CreateDefault(); current.Document = Document();
            current.Messages.Add(new() { MessageId = 291, Signals = [new() { Name = "SecondValue", Value = "Locals.Value" }],
                SignalOutputs = [new() { Name = "SecondValue", TargetVariable = "Locals.ReadValue" }] });
            var copied = (DbcCanSetting)serializer.Deserialize(serializer.Serialize(current), serializer.SettingVersion);
            Assert.True(copied.UseMessageList); Assert.Equal(291u, Assert.Single(copied.Messages).MessageId);
            Assert.Equal("Locals.Value", Assert.Single(copied.Messages[0].Signals).Value);
            Assert.Equal("Locals.ReadValue", Assert.Single(copied.Messages[0].SignalOutputs).TargetVariable);
            Assert.Equal(Text, copied.Document.Text);
        }
    }

    [Theory]
    [InlineData(0u)] [InlineData(291u)]
    public void Current_descriptions_follow_empty_selection_single_ID_and_message_list(uint id)
    {
        foreach (var plugin in Plugins())
        {
            var serializer = plugin.CreateSerializer();
            var current = (DbcCanSetting)serializer.CreateDefault();
            string empty = plugin.GenerateDescription(serializer.Serialize(current));
            Assert.DoesNotContain("0x0", empty);
            if (plugin is not DbcCyclicWritePlugin) Assert.Contains("0 IDs", empty);
            current.UseMessageList = false; current.MessageId = id;
            Assert.Contains($"0x{id:X}", plugin.GenerateDescription(serializer.Serialize(current)));
            current.UseMessageList = true;
            current.MessageId = 0; current.Messages.Add(new() { MessageId = 291 });
            string description = plugin.GenerateDescription(serializer.Serialize(current));
            Assert.DoesNotContain("0x0", description);
            Assert.Contains(plugin is DbcCyclicWritePlugin ? "0x123" : "1 IDs", description);
        }
    }

    [WpfFact]
    public async Task Existing_explicit_single_ID_zero_remains_selected_when_reloading_document()
    {
        var plugin = new DbcWritePlugin();
        var setting = new DbcWriteSetting { UseMessageList = false, MessageId = 0, Document = Document(),
            Signals = [new() { Name = "ZeroValue", Value = "42" }] };
        var step = MakeStep(plugin, setting); var original = step.StepSetting.Setting.ToArray();
        var model = new DbcEditorViewModel(plugin); model.Attach(step); await model.LoadTask; model.Flush();
        Assert.Equal(0u, model.SelectedMessage!.Message.Id); Assert.Equal("42", Assert.Single(model.SignalRows).Value);
        Assert.Equal(original, step.StepSetting.Setting); model.Deactivate();
    }

    internal static void VerifyActualPages()
    {
        var dispatcher = Dispatcher.CurrentDispatcher; var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            // Complete all asynchronous parsing before creating Syncfusion controls. Pumping
            // after their creation can open the SDK license dialog in an unlicensed test host.
            var loading = PrepareViewsAsync(); var frame = new DispatcherFrame(); bool timeout = false;
            var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(12) };
            timer.Tick += (_, _) => { timeout = true; frame.Continue = false; };
            _ = loading.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            timer.Start(); try { if (!loading.IsCompleted) Dispatcher.PushFrame(frame); } finally { timer.Stop(); }
            Assert.False(timeout, "DBC explicit-selection models exceeded preload deadline");
            foreach (var (plugin, step, model) in loading.GetAwaiter().GetResult()) VerifyView(plugin, step, model);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private static async Task<List<(IStepPlugin, Step, DbcEditorViewModel)>> PrepareViewsAsync()
    {
        var result = new List<(IStepPlugin, Step, DbcEditorViewModel)>();
        foreach (var plugin in Plugins())
        {
            var step = MakeStep(plugin, plugin.CreateSerializer().CreateDefault());
            var model = new DbcEditorViewModel(plugin, (_, _, _) => Task.FromResult(Document()));
            model.Attach(step); await model.LoadFileAsync("unused.dbc"); model.Flush();
            Assert.Empty(model.LoadError); Assert.Equal(2, model.Messages.Count);
            result.Add((plugin, step, model));
        }
        return result;
    }

    private static void VerifyView(IStepPlugin plugin, Step step, DbcEditorViewModel model)
    {
        var view = DbcStageQueue.Create(model); Layout(view);
        var signals = Assert.IsType<DataGrid>(view.FindName("SignalsGrid"));
        var configured = Assert.IsType<DataGrid>(view.FindName("MessagesGrid"));
        void AssertEmptySelection()
        {
            Assert.Empty(configured.Items); Assert.Empty(signals.Items); Assert.Empty(model.ConfiguredMessages);
            Assert.Empty(model.SignalRows); Assert.Null(model.SelectedMessage);
            foreach (string selector in new[] { "AddMessageSelector", "ConfiguredMessageSelector", "ProtectionMessageSelector", "MessageSelector" })
                Assert.Equal(-1, Assert.IsType<ComboBox>(view.FindName(selector)).SelectedIndex);
            Assert.False(model.HasActiveMessage);
        }
        Assert.True(Assert.IsType<CheckBox>(view.FindName("UseMessageListCheckBox")).IsChecked);
        AssertEmptySelection();
        Click(view, "BatchAddButton"); Layout(view);
        Click(view, "BatchSelectVisibleButton"); Click(view, "BatchConfirmButton"); Layout(view);
        Assert.Equal(2, configured.Items.Count); Assert.Equal(2, signals.Items.Count); Assert.Equal(2, signals.Items.Groups!.Count);
        Assert.Equal(new uint[] { 0, 291 }, model.SignalRows.Select(row => row.MessageId));
        model.Flush();
        var serializer = plugin.CreateSerializer();
        var saved = (DbcCanSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.True(saved.UseMessageList); Assert.Equal(new uint[] { 0, 291 }, saved.Messages.Select(row => row.MessageId));
        configured.SelectAll(); Click(view, "RemoveMessagesButton"); Layout(view);
        AssertEmptySelection(); model.Flush();
        saved = (DbcCanSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.True(saved.UseMessageList); Assert.Empty(saved.Messages); Assert.Equal(Text, saved.Document.Text);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }

    private static void Click(DbcEditorView view, string name) => Assert.IsType<Button>(view.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Layout(FrameworkElement view) { DbcStageQueue.LayoutAndFinish(view, new Size(1500, 1200)); }
}
