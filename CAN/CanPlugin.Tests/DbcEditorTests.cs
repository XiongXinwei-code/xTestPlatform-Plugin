using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CAN.Dbc;
using CAN.Models;
using CAN.UI.Dbc;
using CAN.UI.Views;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcEditorTests
{
    internal const string DocumentText = """
        VERSION "test"
        BO_ 291 Sample: 8 ECU
         SG_ Voltage : 0|16@1+ (0.1,0) [0|100] "V" Vector__XXX
         SG_ Mode : 16|8@1+ (1,0) [0|3] "" Vector__XXX
         SG_ Alive : 48|8@1+ (1,0) [0|255] "" Vector__XXX
         SG_ Checksum : 56|8@1+ (1,0) [0|255] "" Vector__XXX
        VAL_ 291 Mode 0 "Off" 1 "On" ;
        BO_ 2147483939 Extended: 12 ECU
         SG_ Wide : 0|64@1+ (1,0) [0|0] "count" Vector__XXX
        """;
    private static DbcDocumentSetting Document() => new() { Name = "editor-test.dbc", Text = DocumentText };

    // Called by the shared real-view STA test; Syncfusion caches cannot span dispatchers.
    internal static void VerifyAllEditorWorkflows()
    {
        RunWithDispatcher(VerifyActualDbcPagesAsync);
        VerifyReadProtectionPresentation();
    }

    // These tests create no views or Syncfusion resources. Keep intentional waits on
    // their own Dispatcher: pumping the real-view STA long enough can open the SDK's
    // delayed license dialog in an unlicensed test host and make its modal frame hang.
    [WpfFact]
    public async Task Loading_guards_preserve_identity_and_discard_late_results()
    {
        await VerifyLateParseAsync();
        await VerifyInvalidSavedIdentityAsync();
        await VerifyUncooperativeFileReadAsync();
    }

    private static void RunWithDispatcher(Func<Task> action)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            var task = action();
            if (!task.IsCompleted)
            {
                var frame = new DispatcherFrame();
                bool timedOut = false;
                var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(10) };
                timer.Tick += (_, _) => { timedOut = true; frame.Continue = false; };
                _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
                timer.Start();
                try { Dispatcher.PushFrame(frame); }
                finally { timer.Stop(); }
                Assert.False(timedOut, "DBC 页面测试等待后台加载超过 10 秒");
            }
            task.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    internal static async Task VerifyActualDbcPagesAsync()
    {
        var plugin = new DbcWritePlugin(); var first = MakeStep(plugin, new DbcWriteSetting());
        var next = MakeStep(plugin, new DbcWriteSetting());
        var view = DbcStageQueue.Create(plugin); view.RefreshFromStep(first);
        var temporary = Path.Combine(Path.GetTempPath(), $"xtest-dbc-{Guid.NewGuid():N}.dbc");
        try
        {
            await File.WriteAllTextAsync(temporary, DocumentText, new UTF8Encoding(false));
            await view.ViewModel.LoadFileAsync(temporary);
            Assert.Empty(view.ViewModel.LoadError); Assert.Equal(2, view.ViewModel.Messages.Count);
            Assert.Empty(view.ViewModel.ConfiguredMessages); Assert.Empty(view.ViewModel.SignalRows);
            view.ViewModel.UseMessageList = false; view.ViewModel.SelectedMessage = view.ViewModel.Messages[0];
            Assert.Equal(4, view.ViewModel.SignalRows.Count); Assert.Equal("Voltage", view.ViewModel.SignalRows[0].Name);
            Assert.Equal("V", view.ViewModel.SignalRows[0].Unit); Assert.Contains("Off", view.ViewModel.SignalRows[1].Meaning);
            Layout(view);
            var grid = Assert.IsType<DataGrid>(view.FindName("SignalsGrid"));
            var row = view.ViewModel.SignalRows[0];
            Assert.False(row.Selected); row.Selected = true;
            var valueColumn = grid.Columns.Single(column => column.Header?.ToString() == "发送值表达式");
            grid.ScrollIntoView(row, valueColumn); view.UpdateLayout();
            var input = DbcRenderTests.EditValue(grid, row, valueColumn, view);
            input.GetType().GetProperty("ScriptText")!.SetValue(input, "12.5");
            view.ViewModel.Protection!.RcEnabled = true;
            view.ViewModel.Protection.RcStartByte = 6; view.ViewModel.Protection.RcEndByte = 6;
            view.ViewModel.Protection.CrcEnabled = true;
            view.ViewModel.Flush();
            var saved = (DbcWriteSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, first.StepSetting.SettingVersion);
            Assert.Equal("12.5", saved.Signals.Single(s => s.Name == "Voltage").Value);
            Assert.Equal(DocumentText, saved.Document.Text); Assert.True(saved.Protection.RcEnabled); Assert.True(saved.Protection.CrcEnabled);
            Assert.False(view.ViewModel.SignalRows.Single(s => s.Name == "Alive").IsEditable);
            Assert.Equal("由 RC/CRC/Data ID 填写", view.ViewModel.SignalRows.Single(s => s.Name == "Checksum").ProtectionNote);
            var oldProtection = view.ViewModel.Protection;
            view.ViewModel.SignalRows[1].Value = "1";
            view.RefreshFromStep(next); await view.ViewModel.LoadTask;
            saved = (DbcWriteSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, first.StepSetting.SettingVersion);
            Assert.Equal("1", saved.Signals.Single(s => s.Name == "Mode").Value);
            var nextBytes = next.StepSetting.Setting.ToArray(); oldProtection!.RcInitialValue = 44; view.ViewModel.Flush();
            Assert.Equal(nextBytes, next.StepSetting.Setting);
            view.RefreshFromStep(first); await view.ViewModel.LoadTask; Layout(view);
            Assert.Equal("12.5", view.ViewModel.SignalRows[0].Value);
            var selector = Assert.IsType<ComboBox>(view.FindName("MessageSelector")); selector.SelectedIndex = 1;
            Assert.Equal("Wide", Assert.Single(view.ViewModel.SignalRows).Name);
            view.ViewModel.Flush(); saved = (DbcWriteSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, first.StepSetting.SettingVersion);
            Assert.Equal(CanFrameType.Extended, saved.FrameType); Assert.Equal(0x123u, saved.MessageId);
        }
        finally { view.ViewModel.Deactivate(); File.Delete(temporary); }

        foreach (var other in new IStepPlugin[] { new DbcReadPlugin(), new DbcCyclicWritePlugin(), new DbcCapturePlugin() })
        {
            var setting = (DbcSourceSetting)other.CreateSerializer().CreateDefault(); setting.Document = Document();
            if (setting is DbcCanSetting can) { can.UseMessageList = false; can.MessageId = 0x123; }
            var current = DbcStageQueue.Create(other); current.RefreshFromStep(MakeStep(other, setting));
            await current.ViewModel.LoadTask; Layout(current);
            Assert.Empty(current.ViewModel.LoadError); Assert.Equal(2, current.ViewModel.Messages.Count);
            Assert.Equal(4, current.ViewModel.SignalRows.Count); Assert.True(current.ViewModel.HasConnection);
            Assert.Contains("DBC", current.ViewModel.Title, StringComparison.OrdinalIgnoreCase);
            current.ViewModel.UseMessageList = true; Layout(current);
            Assert.Single(current.ViewModel.ConfiguredMessages);
            var messageGrid = Assert.IsType<DataGrid>(current.FindName("MessagesGrid"));
            Assert.Equal(Visibility.Visible, messageGrid.Visibility);
            Assert.IsType<ComboBox>(current.FindName("AddMessageSelector")).SelectedIndex = 1;
            current.ViewModel.AddSelectedMessage(); Layout(current);
            Assert.Equal(5, current.ViewModel.SignalRows.Count);
            Assert.Equal("Wide", Assert.Single(current.ViewModel.SignalRows, row => row.IsExtended).Name);
            messageGrid.SelectedIndex = 0; Layout(current);
            Assert.Equal(5, current.ViewModel.SignalRows.Count);
            Assert.Null(current.FindName("ProtectionEditor"));
            Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(current.Content).SelectedIndex = 1; Layout(current);
            Assert.Same(current.ViewModel.Protection, Assert.IsType<PayloadProtectionEditorView>(current.FindName("ProtectionEditor")).DataContext);
            current.ViewModel.Deactivate();
        }
        await DbcOutputEditorTests.VerifyActualOutputPagesAsync();
    }

    private static async Task VerifyLateParseAsync()
    {
        var plugin = new DbcWritePlugin(); var model = new DbcEditorViewModel(plugin);
        var old = MakeStep(plugin, new DbcWriteSetting { UseMessageList = false, Document = Document(), MessageId = 0x123 });
        var fresh = MakeStep(plugin, new DbcWriteSetting());
        model.Attach(old); var previousLoad = model.LoadTask;
        model.Attach(fresh); await previousLoad; await model.LoadTask; model.Flush();
        Assert.Empty(model.Messages); Assert.Empty(model.SignalRows); Assert.Equal("", model.DocumentName);
        var saved = (DbcWriteSetting)plugin.CreateSerializer().Deserialize(fresh.StepSetting.Setting, fresh.StepSetting.SettingVersion);
        Assert.Empty(saved.Document.Text); Assert.Empty(saved.Signals); model.Deactivate();
    }

    private static async Task VerifyInvalidSavedIdentityAsync()
    {
        var plugin = new DbcWritePlugin(); var model = new DbcEditorViewModel(plugin);
        var step = MakeStep(plugin, new DbcWriteSetting { UseMessageList = false, Document = Document(), MessageId = 0x777 });
        var original = step.StepSetting.Setting.ToArray(); model.Attach(step); await model.LoadTask; model.Flush();
        Assert.Null(model.SelectedMessage); Assert.Empty(model.SignalRows); Assert.Contains("未自动替换", model.LoadError);
        Assert.Equal(original, step.StepSetting.Setting); model.Deactivate();
    }

    private static void VerifyReadProtectionPresentation()
    {
        var panel = new PayloadProtectionEditorView { DataContext = new CanPayloadProtectionSetting { RcEnabled = true }, ReadMode = true };
        Layout(panel);
        Assert.Equal(Visibility.Collapsed, Assert.IsType<TextBox>(panel.FindName("RcInitialValueBox")).Visibility);
        Assert.Contains(Descendants(panel).OfType<TextBlock>(), label => label.Text == "字段起始字节:");
        panel.ReadMode = false; Layout(panel);
        Assert.Equal(Visibility.Visible, Assert.IsType<TextBox>(panel.FindName("RcInitialValueBox")).Visibility);
        Assert.Contains(Descendants(panel).OfType<TextBlock>(), label => label.Text == "写入起始字节:");
    }

    private static async Task VerifyUncooperativeFileReadAsync()
    {
        var reading = new TaskCompletionSource<DbcDocumentSetting>(TaskCreationOptions.RunContinuationsAsynchronously);
        var plugin = new DbcReadPlugin(); var model = new DbcEditorViewModel(plugin, (_, _, _) => reading.Task);
        var step = MakeStep(plugin, new DbcReadSetting { UseMessageList = false, Document = Document(), MessageId = 0x123 });
        model.Attach(step); await model.LoadTask;
        model.TimeoutMs = 25;
        await model.LoadFileAsync("unresponsive.dbc");
        Assert.False(model.IsLoading); Assert.True(model.CanSelect); Assert.Contains("超时", model.LoadError);
        Assert.Equal("editor-test.dbc", model.DocumentName);
        reading.SetResult(new() { Name = "late.dbc", Text = DocumentText });
        await Task.Yield(); model.Flush();
        var saved = (DbcReadSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal("editor-test.dbc", saved.Document.Name); model.Deactivate();
    }

    [Fact]
    public async Task Editors_validate_literal_values_wrong_identity_and_protection_boundaries()
    {
        var setting = new DbcWriteSetting { UseMessageList = false, Document = Document(), MessageId = 0x123,
            Signals = [new() { Name = "Voltage", Value = "12.55" }, new() { Name = "Mode", Value = "0" }, new() { Name = "Alive", Value = "0" }, new() { Name = "Checksum", Value = "0" }] };
        var editor = new DbcWriteEditorPlugin(); var plugin = new DbcWritePlugin();
        var errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "DBC_SIGNAL_VALUE");
        setting.Signals[0].Value = "12.5"; setting.Protection.RcEnabled = true;
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "DBC_SIGNAL_PROTECTION_OVERLAP");
        setting.Protection.RcEnabled = false; setting.FrameType = CanFrameType.Extended;
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "DBC_SIGNAL_UNKNOWN");
    }

    [Fact]
    public async Task Capture_validates_all_messages_not_only_the_selected_one()
    {
        var setting = new DbcCaptureSetting { UseMessageList = false, Document = Document(), MessageId = 0x123, AllDbcMessages = true,
            FrameFormat = DbcFrameFormat.Classic, ResultVariable = "Locals.Result" };
        var errors = await new DbcCaptureEditorPlugin().ValidateWithContextAsync(Round2XcpContractTests.Context(new DbcCapturePlugin(), setting));
        Assert.Contains(errors, e => e.Code == "DBC_FRAME_FORMAT");
    }

    [WpfFact]
    public async Task Multi_message_editor_preserves_row_expressions_protection_and_single_mode_identity()
    {
        var plugin = new DbcWritePlugin();
        var single = new DbcWriteSetting { UseMessageList = false, Document = Document(), MessageId = 0x123,
            Signals = [new() { Name = "Voltage", Value = "Locals.Voltage" }],
            Protection = new() { RcEnabled = true, RcStartByte = 6, RcEndByte = 6, RcInitialValue = 12 } };
        var step = MakeStep(plugin, single); var model = new DbcEditorViewModel(plugin);
        model.Attach(step); await model.LoadTask;
        Assert.True(model.SignalRows.First(s => s.Name == "Voltage").Selected);
        model.UseMessageList = true;
        var first = Assert.Single(model.ConfiguredMessages).Item;
        model.MessageToAdd = model.Messages.Single(m => m.Message.Extended);
        model.AddSelectedMessage();
        Assert.Equal(2, model.ConfiguredMessages.Count);
        var wide = model.SignalRows.Single(row => row.IsExtended);
        Assert.False(wide.Selected); Assert.Contains("原始值 0", wide.DefaultValue);
        wide.Selected = true; wide.ValueKind = (int)DbcValueKind.Raw; wide.Value = "Locals.Wide + 1";
        model.FrameFormat = (int)DbcFrameFormat.Fd;
        model.Protection!.CrcEnabled = true; model.Protection.CrcStartByte = 11; model.Protection.CrcEndByte = 11;
        model.SelectedConfiguredMessage = model.ConfiguredMessages.Single(r => ReferenceEquals(r.Item, first));
        Assert.Equal("Locals.Voltage", model.SignalRows.First(s => s.Name == "Voltage").Value);
        Assert.Equal(12u, model.Protection!.RcInitialValue); Assert.False(model.Protection.CrcEnabled);
        model.Flush(); model.Deactivate();
        var saved = (DbcWriteSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal(2, saved.Messages.Count); Assert.True(saved.UseMessageList);
        Assert.Equal("Locals.Wide + 1", Assert.Single(saved.Messages[1].Signals).Value);
        Assert.Equal("Locals.Voltage", Assert.Single(saved.Signals).Value);
        var reopened = new DbcEditorViewModel(plugin); var before = step.StepSetting.Setting.ToArray();
        reopened.Attach(step); await reopened.LoadTask;
        reopened.SelectedConfiguredMessage = reopened.ConfiguredMessages[1];
        Assert.Equal("Locals.Wide + 1", reopened.SignalRows.Single(row => row.IsExtended).Value);
        Assert.True(reopened.Protection!.CrcEnabled); Assert.Equal(11, reopened.Protection.CrcStartByte);
        reopened.SelectedConfiguredMessage = reopened.ConfiguredMessages[0];
        Assert.True(reopened.Protection!.RcEnabled); reopened.Flush(); Assert.Equal(before, step.StepSetting.Setting);
        reopened.UseMessageList = false;
        Assert.Equal("Locals.Voltage", reopened.SignalRows.First(s => s.Name == "Voltage").Value);
        reopened.Deactivate();
    }

    [WpfFact]
    public async Task Multi_message_reload_keeps_unknown_rows_and_capture_modes_are_explicit()
    {
        var plugin = new DbcCapturePlugin(); var setting = new DbcCaptureSetting
        { UseMessageList = false, Document = Document(), MessageId = 0x123, AllDbcMessages = true };
        var step = MakeStep(plugin, setting); var model = new DbcEditorViewModel(plugin);
        model.Attach(step); await model.LoadTask; Assert.True(model.AllDbcMessages);
        model.UseMessageList = true; Assert.False(model.AllDbcMessages); Assert.False(model.CanCaptureAll);
        model.AllDbcMessages = true; Assert.False(model.AllDbcMessages);
        model.ConfiguredMessages[0].Item.MessageId = 0x777; model.Flush();
        var original = step.StepSetting.Setting.ToArray();
        await model.ReloadEmbeddedAsync(); model.Flush();
        Assert.Contains("未自动替换", model.LoadError); Assert.Null(model.SelectedMessage);
        Assert.Equal(0x777u, Assert.Single(model.ConfiguredMessages).Item.MessageId);
        Assert.Equal(original, step.StepSetting.Setting); model.Deactivate();
    }

    [WpfTheory]
    [InlineData("select", false)] [InlineData("select", true)]
    [InlineData("add", false)] [InlineData("add", true)]
    [InlineData("remove", false)] [InlineData("remove", true)]
    [InlineData("enable", false)] [InlineData("enable", true)]
    public async Task Save_reentry_rebinds_multi_message_edits_and_does_not_modify_a_new_step(string action, bool switchStep)
    {
        var plugin = new DbcWritePlugin(); var setting = new DbcWriteSetting
        {
            Document = new() { Name = "reentry.dbc", Text = DocumentText + "\nBO_ 292 Third: 8 ECU\n SG_ Value : 0|8@1+ (1,0) [0|255] \"\" Vector__XXX\n" },
            MessageId = 0x123, UseMessageList = action != "enable", Signals = [new() { Name = "Voltage", Value = "10" }],
            Messages = action == "enable" ? [] : [new() { MessageId = 0x123, Signals = [new() { Name = "Voltage", Value = "10" }] },
                new() { MessageId = 0x123, FrameType = CanFrameType.Extended, Signals = [new() { Name = "Wide", Value = "20" }] }]
        };
        var step = MakeStep(plugin, setting); var next = MakeStep(plugin, new DbcWriteSetting { ConnectionName = "\"NEW\"" });
        var nextBytes = next.StepSetting.Setting.ToArray(); var model = new DbcEditorViewModel(plugin);
        model.Attach(step); await model.LoadTask;
        model.SignalRows.First(s => s.Name == "Voltage").Value = "12.5";
        var target = action == "select" ? model.ConfiguredMessages[1] : null;
        if (action == "add") model.MessageToAdd = model.Messages.Single(m => m.Message.Id == 292);
        QueueSynchronousRefresh(model, switchStep ? next : step);
        if (action == "select") model.SelectedConfiguredMessage = target;
        else if (action == "add") model.AddSelectedMessage();
        else if (action == "remove") model.RemoveSelectedMessage();
        else model.UseMessageList = true;
        await model.LoadTask;
        if (switchStep)
        {
            Assert.Equal("\"NEW\"", model.ConnectionName); Assert.True(model.UseMessageList);
            Assert.Empty(model.ConfiguredMessages); Assert.Empty(model.SignalRows); Assert.Null(model.SelectedMessage);
            model.Flush(); Assert.Equal(nextBytes, next.StepSetting.Setting);
        }
        else
        {
            if (action == "select")
            {
                Assert.Equal("Wide", model.SignalRows.Single(row => row.IsExtended).Name);
                model.SignalRows.Single(row => row.IsExtended).Value = "23";
            }
            model.Flush();
            var saved = (DbcWriteSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.True(saved.UseMessageList);
            if (action == "remove") Assert.Equal(CanFrameType.Extended, Assert.Single(saved.Messages).FrameType);
            else
            {
                Assert.Equal("12.5", saved.Messages[0].Signals.Single(s => s.Name == "Voltage").Value);
                if (action == "select") Assert.Equal("23", saved.Messages[1].Signals.Single(s => s.Name == "Wide").Value);
                if (action == "add") Assert.Equal(292u, saved.Messages[2].MessageId);
                if (action == "enable") Assert.Single(saved.Messages);
            }
        }
        model.Deactivate();
    }

    [WpfTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task Attach_during_save_keeps_a_newer_step_but_allows_same_step_refresh(bool selectNewerStep)
    {
        var plugin = new DbcWritePlugin(); var model = new DbcEditorViewModel(plugin);
        var old = MakeStep(plugin, new DbcWriteSetting { ConnectionName = "\"OLD\"" });
        var requested = MakeStep(plugin, new DbcWriteSetting { ConnectionName = "\"REQUESTED\"" });
        var newer = MakeStep(plugin, new DbcWriteSetting { ConnectionName = "\"NEWER\"" });
        model.Attach(old); model.ConnectionName = "\"SAVED\"";
        QueueSynchronousRefresh(model, selectNewerStep ? newer : old);
        model.Attach(requested); await model.LoadTask; model.Flush();
        Assert.Equal(selectNewerStep ? "\"NEWER\"" : "\"REQUESTED\"", model.ConnectionName);
        var saved = (DbcWriteSetting)plugin.CreateSerializer().Deserialize(old.StepSetting.Setting, old.StepSetting.SettingVersion);
        Assert.Equal("\"SAVED\"", saved.ConnectionName); model.Deactivate();
    }

    private static void QueueSynchronousRefresh(DbcEditorViewModel model, Step refreshedStep)
    {
        model.ExecuteCommand = (_, apply) => { apply(); model.Attach(refreshedStep); };
        model.TimeoutMs++;
    }

    [Fact]
    public async Task Multi_message_validation_checks_each_enabled_identity_and_protection()
    {
        var plugin = new DbcWritePlugin(); var editor = new DbcWriteEditorPlugin();
        var setting = new DbcWriteSetting { Document = Document(), UseMessageList = true,
            Messages = [new() { MessageId = 0x123 }, new() { MessageId = 0x123, FrameType = CanFrameType.Extended,
                FrameFormat = DbcFrameFormat.Classic }] };
        var errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "DBC_FRAME_FORMAT");
        setting.Messages[1].FrameFormat = DbcFrameFormat.Fd;
        setting.Messages[1].Protection.CrcEnabled = true; setting.Messages[1].Protection.CrcStartByte = 30;
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "DBC_PROTECTION");
        setting.Messages[1].Protection.CrcEnabled = false; setting.Messages[1].FrameType = CanFrameType.Standard;
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "DBC_MESSAGE_DUPLICATE");
        setting.Messages[1].MessageId = 0x777;
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "DBC_MESSAGE");
        setting.Messages[1].Enabled = false;
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.DoesNotContain(errors, e => e.Code is "DBC_MESSAGE" or "DBC_MESSAGE_DUPLICATE" or "DBC_PROTECTION");
        var capture = new DbcCaptureSetting { Document = Document(), UseMessageList = true, AllDbcMessages = true, Messages = setting.Messages };
        errors = await new DbcCaptureEditorPlugin().ValidateWithContextAsync(Round2XcpContractTests.Context(new DbcCapturePlugin(), capture));
        Assert.Contains(errors, e => e.Code == "DBC_SELECTION_MODE");
    }

    [Fact]
    public async Task Missing_unselected_and_known_inactive_signals_do_not_require_expressions()
    {
        var plugin = new DbcWritePlugin(); var editor = new DbcWriteEditorPlugin();
        var setting = new DbcWriteSetting { UseMessageList = false, Document = Document(), MessageId = 0x123,
            Signals = [new() { Name = "Voltage", Selected = false, Value = "", ValueKind = DbcValueKind.Raw }] };
        var errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.DoesNotContain(errors, e => e.Code.StartsWith("DBC_SIGNAL", StringComparison.Ordinal));
        setting.Signals[0].Selected = true;
        setting.Signals[0].ValueKind = (DbcValueKind)99;
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting, untrustedInput: true));
        Assert.Contains(errors, e => e.Code == "DBC_SIGNAL_EXPRESSION");
        Assert.Contains(errors, e => e.Code == "DBC_SIGNAL_KIND");
        setting.Document.Text = """
            BO_ 291 Multiplexed: 8 ECU
             SG_ Selector M : 0|8@1+ (1,0) [0|255] "" Vector__XXX
             SG_ Branch0 m0 : 8|8@1+ (1,0) [0|255] "" Vector__XXX
             SG_ Branch1 m1 : 8|8@1+ (1,0) [0|255] "" Vector__XXX
            """;
        setting.Signals = [new() { Name = "Branch1", Selected = true, Value = "" }];
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.DoesNotContain(errors, e => e.Code == "DBC_SIGNAL_EXPRESSION");
        setting.Signals.Add(new() { Name = "Selector", Value = "1" });
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "DBC_SIGNAL_EXPRESSION");
        setting.Signals[1].Value = "Locals.DynamicMux";
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "DBC_SIGNAL_EXPRESSION");
    }

    [Fact]
    public async Task Receive_protection_does_not_validate_unused_counter_generation_rules()
    {
        var setting = new DbcReadSetting { UseMessageList = false, Document = Document(), MessageId = 0x123,
            Protection = new() { RcEnabled = true, RcStartByte = 6, RcEndByte = 6, RcInitialValue = 999, RcMinimum = 20, RcMaximum = 10 } };
        var plugin = new DbcReadPlugin(); var editor = new DbcReadEditorPlugin();
        var errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.DoesNotContain(errors, e => e.Code == "DBC_PROTECTION");
        setting.Protection.RcEndByte = 9;
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "DBC_PROTECTION");
    }

    private static Step MakeStep(IStepPlugin plugin, object setting)
    {
        var step = new Step(); var serializer = plugin.CreateSerializer();
        step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion;
        return step;
    }
    private static void Layout(FrameworkElement view)
    { DbcStageQueue.LayoutAndFinish(view, new Size(1200, 900)); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var nested in Descendants(VisualTreeHelper.GetChild(root, index))) yield return nested;
    }
}
