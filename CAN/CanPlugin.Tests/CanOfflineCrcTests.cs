using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using CAN.Helpers;
using CAN.Models;
using CAN.UI;
using CAN.UI.Views;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

// Added before the implementation: repeatable targets are the AUTOSAR vector,
// no CAN resource, no payload mutation, and no result publication after cancellation.
[Collection("CAN blocked I/O")]
public sealed class CanOfflineCrcTests
{
    [Fact]
    public void Public_profile2_function_appends_one_id_to_selected_data_without_modifying_it()
    {
        byte[] selected = [1, 0, 0, 0, 0, 0, 0];
        byte[] original = selected.ToArray();
        Assert.Equal(0x1B, CanCrcFunctions.E2E_Crc8(selected, 0x02)); // R24-11 table 6.17.
        Assert.Equal(original, selected);
    }

    [Theory]
    [InlineData(CanCrcDataIdMode.Profile1Both, 0x00, 0xCC)]
    [InlineData(CanCrcDataIdMode.Profile1Alt, 0x01, 0x02)]
    [InlineData(CanCrcDataIdMode.Profile1Nibble, 0x10, 0x2A)]
    public void Generic_function_uses_existing_counter_and_nibble_without_generating_them(CanCrcDataIdMode mode, byte counterByte, byte expected)
    {
        var setting = Protection(mode); setting.RcEnabled = true; setting.RcStartByte = 1; setting.RcEndByte = 1;
        setting.RcUseBitField = true; setting.RcBitLength = 4; setting.RcInitialValue = uint.MaxValue;
        setting.RcMaximum = 99; setting.RcStep = long.MaxValue;
        byte[] payload = [0xAA, counterByte, 0, 0, 0, 0, 0, 0];
        byte[] original = payload.ToArray();
        Assert.Equal((ulong)expected, CanCrcFunctions.Calculate(payload, setting));
        Assert.Equal(original, payload); Assert.Equal(uint.MaxValue, setting.RcInitialValue);
    }

    [Fact]
    public void Pure_calculation_rejects_unresolved_table_expressions_and_disabled_crc()
    {
        Assert.Throws<ArgumentException>(() => CanCrcFunctions.Calculate(new byte[8], new()));
        var s = Protection(CanCrcDataIdMode.Profile2); s.CrcDataIdTableUseExpression = true; s.CrcDataIdTableExpression = "Locals.Ids";
        Assert.Throws<ArgumentException>(() => CanCrcFunctions.Calculate(new byte[8], s));
    }

    [Fact]
    public async Task Offline_step_completes_without_a_CAN_connection_and_can_skip_result_variable()
    {
        var s = new CanCrcCalculateSetting { Data = "\"00 01 00 00 00 00 00 00\"", Protection = Protection(CanCrcDataIdMode.Profile2), ResultVariable = "Locals.Crc" };
        var (context, state) = Context(s); state.Variables["Locals.Crc"] = "old";
        var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Passed, result.StepResult.Status);
        Assert.Equal("0x1B", result.StepResult.Value); Assert.Equal("0x1B", state.Variables["Locals.Crc"]);
        Assert.Null(state.Adapter); Assert.False(state.LookedUp.Task.IsCompleted);
        s.ResultVariable = "";
        result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Passed, result.StepResult.Status); Assert.Equal("0x1B", result.StepResult.Value);
    }

    [Fact]
    public async Task Offline_step_resolves_data_id_table_expression_and_checks_result_variable_before_evaluation()
    {
        var s = new CanCrcCalculateSetting { Data = "\"00 01 00 00 00 00 00 00\"", Protection = Protection(CanCrcDataIdMode.Profile2), ResultVariable = "Locals.Crc" };
        s.Protection.CrcDataIdTableUseExpression = true;
        s.Protection.CrcDataIdTableExpression = "new byte[] { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16 }";
        s.Protection.CrcDataIdTable = [];
        var (context, state) = Context(s); state.Variables["Locals.Crc"] = "old";
        var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Passed, result.StepResult.Status); Assert.Equal("0x1B", result.StepResult.Value);
        state.Variables["Locals.Crc"] = 10;
        result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Contains("字符串", result.StepResult.Error!.Message);
        Assert.Equal(10, state.Variables["Locals.Crc"]);
    }

    [Fact]
    public async Task Cancelled_offline_evaluation_does_not_publish_a_late_result()
    {
        var s = new CanCrcCalculateSetting { Data = "Locals.DataIds", ResultVariable = "Locals.Crc" };
        var (context, state) = Context(s); state.Variables["Locals.DataIds"] = "00 00 00 00 00 00 00 00"; state.Variables["Locals.Crc"] = "old";
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.BeforeRead = () => { entered.TrySetResult(); release.Wait(); };
        using var stop = new CancellationTokenSource();
        var pending = state.Plugin.CreateExecutor().ExecuteAsync(context, stop.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); stop.Cancel();
            Assert.Equal(TestStatus.Aborted, (await pending.WaitAsync(TimeSpan.FromSeconds(2))).StepResult.Status);
        }
        finally { release.Set(); }
        await Task.Delay(100);
        Assert.Equal("old", state.Variables["Locals.Crc"]);
    }

    [Fact]
    public async Task Internal_evaluation_deadline_is_Error_and_cannot_publish_a_late_result()
    {
        var s = new CanCrcCalculateSetting { Data = "Locals.DataIds", ResultVariable = "Locals.Crc", TimeoutMs = 300 };
        var (context, state) = Context(s); state.Variables["Locals.DataIds"] = "00 00 00 00 00 00 00 00"; state.Variables["Locals.Crc"] = "old";
        using var release = new ManualResetEventSlim();
        state.BeforeRead = () => release.Wait();
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("300 ms", result.StepResult.Error!.Message);
        }
        finally { release.Set(); }
        await Task.Delay(100);
        Assert.Equal("old", state.Variables["Locals.Crc"]);
    }

    [Fact]
    public async Task Offline_validator_requires_no_open_step_and_accepts_expression_source_without_inline_table()
    {
        var s = new CanCrcCalculateSetting { Protection = Protection(CanCrcDataIdMode.Profile2) };
        s.Protection.CrcDataIdTableUseExpression = true; s.Protection.CrcDataIdTableExpression = "new byte[16]"; s.Protection.CrcDataIdTable = [];
        var errors = await new CanCrcCalculateEditorPlugin().ValidateWithContextAsync(Round2XcpContractTests.Context(new CanCrcCalculatePlugin(), s));
        Assert.DoesNotContain(errors, error => error.Severity.ToString() == "Error");
        s.TimeoutMs = 0;
        errors = await new CanCrcCalculateEditorPlugin().ValidateWithContextAsync(Round2XcpContractTests.Context(new CanCrcCalculatePlugin(), s));
        Assert.Contains(errors, error => error.Code == "CAN_CRC_TIMEOUT");
    }

    [Fact]
    public async Task Offline_validator_ignores_transmit_counter_generation_rules_but_checks_payload_layout()
    {
        var s = new CanCrcCalculateSetting { Protection = Protection(CanCrcDataIdMode.Profile2) };
        s.Protection.RcEnabled = true; s.Protection.RcStartByte = 1; s.Protection.RcEndByte = 1;
        s.Protection.RcUseBitField = true; s.Protection.RcBitLength = 4;
        s.Protection.RcInitialValue = uint.MaxValue; s.Protection.RcMaximum = 99; s.Protection.RcStep = long.MaxValue;
        var errors = await new CanCrcCalculateEditorPlugin().ValidateWithContextAsync(Round2XcpContractTests.Context(new CanCrcCalculatePlugin(), s));
        Assert.DoesNotContain(errors, error => error.Severity.ToString() == "Error");
        s.Data = "\"00\"";
        errors = await new CanCrcCalculateEditorPlugin().ValidateWithContextAsync(Round2XcpContractTests.Context(new CanCrcCalculatePlugin(), s));
        Assert.Contains(errors, error => error.Code == "CAN_CRC_SETTING");
    }

    // Called from a standalone STA fact, with no Syncfusion controls instantiated.
    [StaFact]
    public void Offline_view_model_flushes_old_step_and_detaches_protection_events()
    {
        var plugin = new CanCrcCalculatePlugin();
        var first = new Step(); first.StepSetting.Setting = plugin.CreateSerializer().Serialize(new CanCrcCalculateSetting());
        var second = new Step(); second.StepSetting.Setting = plugin.CreateSerializer().Serialize(new CanCrcCalculateSetting());
        var vm = new CAN.UI.ViewModels.CanCrcCalculateViewModel(); vm.Attach(first);
        vm.Data = "\"01 02\""; vm.TimeoutMs = 1234; var original = vm.Protection!;
        vm.Protection!.CrcDataIdEnabled = true; vm.Protection.CrcDataIdHex = "12";
        vm.Attach(second);
        var stored = (CanCrcCalculateSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1);
        Assert.Equal("\"01 02\"", stored.Data); Assert.Equal(1234, stored.TimeoutMs); Assert.Equal("12", stored.Protection.CrcDataIdHex);
        var untouched = second.StepSetting.Setting.ToArray(); original.CrcDataIdHex = "FF"; vm.Flush();
        Assert.Equal(untouched, second.StepSetting.Setting); vm.Deactivate();
    }

    // Invoked by EditorRuntimeTests on its existing STA; do not create a second Syncfusion dispatcher.
    internal static void VerifyActualPage()
    {
        var plugin = new CanCrcCalculatePlugin();
        var first = EditorStep(plugin, new CanCrcCalculateSetting { Protection = Protection(CanCrcDataIdMode.Profile2) });
        var second = EditorStep(plugin, new CanCrcCalculateSetting());
        var file = new SequenceFile(); var position = new EditPosition();
        var view = new CanCrcCalculateEditorView { SequenceFile = file, EditPosition = position };
        view.RefreshFromStep(first); Layout(view);
        var expression = Assert.IsAssignableFrom<FrameworkElement>(view.FindName("DataExpression"));
        VerifyContext(expression, file, position);
        EditExpression(expression, "\"00 01 02 03 04 05 06 07\"");
        Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(view.Content).SelectedIndex = 1; Layout(view);
        var editor = Assert.IsType<PayloadProtectionEditorView>(view.FindName("ProtectionEditor"));
        Assert.True(editor.ReadMode); Assert.Same(file, editor.SequenceFile); Assert.Same(position, editor.EditPosition);
        var oldRow = editor.DataIdRows[0];
        Assert.IsType<CheckBox>(editor.FindName("CrcDataIdTableUseExpressionCheckBox")).IsChecked = true; Layout(view);
        Assert.True(view.ViewModel.Protection!.CrcDataIdTableUseExpression);
        Assert.Equal(Visibility.Collapsed, Assert.IsType<StackPanel>(editor.FindName("InlineDataIdTablePanel")).Visibility);
        var tableExpression = Assert.IsAssignableFrom<FrameworkElement>(editor.FindName("CrcDataIdTableExpressionBox"));
        VerifyContext(tableExpression, file, position);
        EditExpression(tableExpression, "Locals.CrcIds");
        view.RefreshFromStep(second); Layout(view);
        var stored = (CanCrcCalculateSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1);
        Assert.Equal("\"00 01 02 03 04 05 06 07\"", stored.Data);
        Assert.True(stored.Protection.CrcDataIdTableUseExpression); Assert.Equal("Locals.CrcIds", stored.Protection.CrcDataIdTableExpression);
        var unchanged = second.StepSetting.Setting.ToArray(); oldRow.Value = "FE"; view.ViewModel.Flush();
        Assert.Equal(unchanged, second.StepSetting.Setting);
        view.RefreshFromStep(first); Layout(view);
        Assert.IsType<CheckBox>(editor.FindName("CrcDataIdTableUseExpressionCheckBox")).IsChecked = false; Layout(view);
        var unloadRow = editor.DataIdRows[0];
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        unloadRow.Value = "EF"; view.ViewModel.Data = "\"FF\""; view.ViewModel.Flush();
        stored = (CanCrcCalculateSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1);
        Assert.Equal("01", stored.Protection.CrcDataIdTable[0]); Assert.Equal("\"00 01 02 03 04 05 06 07\"", stored.Data);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        editor.DataIdRows[0].Value = "CD"; view.ViewModel.Flush();
        Assert.Equal("CD", ((CanCrcCalculateSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1)).Protection.CrcDataIdTable[0]);
        view.ViewModel.Deactivate();

        VerifyDynamicCyclicControls(file, position);
    }

    private static void VerifyDynamicCyclicControls(SequenceFile file, EditPosition position)
    {
        var plugin = new CanCyclicSendStartPlugin();
        var first = EditorStep(plugin, new CanCyclicSendStartSetting { Messages = [new()] });
        var second = EditorStep(plugin, new CanCyclicSendStartSetting { Messages = [new()] });
        var view = new CanCyclicSendStartEditorView { SequenceFile = file, EditPosition = position };
        view.RefreshFromStep(first); Layout(view, 1800);
        var grid = Assert.IsType<DataGrid>(view.FindName("MessagesGrid"));
        var row = view.ViewModel.Messages[0]; grid.SelectedItem = row; grid.ScrollIntoView(row); grid.UpdateLayout();
        foreach (var (name, value, expectedType) in new[] { ("EnabledExpressionBox", "Locals.SendEnabled", "Boolean"), ("CycleTimeExpressionBox", "Locals.PeriodMs", "Int32") })
        {
            var expression = Descendants(grid).OfType<FrameworkElement>().Single(e => e.Name == name);
            VerifyContext(expression, file, position);
            Assert.Equal(expectedType, expression.GetType().GetProperty("ExpectedResultType")!.GetValue(expression)!.ToString());
            EditExpression(expression, value);
        }
        Assert.IsType<TextBox>(view.FindName("ControlRefreshMsBox")).Text = "35";
        Assert.IsType<TextBox>(view.FindName("ExpressionTimeoutMsBox")).Text = "1800";
        view.RefreshFromStep(second);
        var stored = (CanCyclicSendStartSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1);
        Assert.Equal("Locals.SendEnabled", stored.Messages[0].EnabledExpression);
        Assert.Equal("Locals.PeriodMs", stored.Messages[0].CycleTimeMsExpression);
        Assert.Equal(35, stored.ControlRefreshMs); Assert.Equal(1800, stored.ExpressionTimeoutMs);
        var unchanged = second.StepSetting.Setting.ToArray(); row.CycleTimeMsExpression = "999"; view.ViewModel.Flush();
        Assert.Equal(unchanged, second.StepSetting.Setting);
    }

    private static Step EditorStep(IStepPlugin plugin, object setting)
    {
        var step = new Step(); step.StepSetting.Setting = plugin.CreateSerializer().Serialize(setting); step.StepSetting.SettingVersion = 1;
        return step;
    }
    private static void Layout(FrameworkElement view, int width = 1000)
    { view.Measure(new Size(width, 900)); view.Arrange(new Rect(0, 0, width, 900)); view.UpdateLayout(); }
    private static void VerifyContext(FrameworkElement expression, SequenceFile file, EditPosition position)
    {
        Assert.Equal("ExpressionTextBox.ExpressionTextBox", expression.GetType().FullName);
        Assert.Same(file, expression.GetType().GetProperty("SequenceFile")!.GetValue(expression));
        Assert.Same(position, expression.GetType().GetProperty("EditPosition")!.GetValue(expression));
    }
    private static void EditExpression(FrameworkElement expression, string value)
    {
        expression.GetType().GetProperty("ScriptText")!.SetValue(expression, value);
        var property = (DependencyProperty)expression.GetType().GetField("ScriptTextProperty")!.GetValue(null)!;
        if (BindingOperations.GetMultiBindingExpression(expression, property) is { } multi) multi.UpdateSource();
        else BindingOperations.GetBindingExpression(expression, property)!.UpdateSource();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static CanPayloadProtectionSetting Protection(CanCrcDataIdMode mode) => new()
    {
        CrcEnabled = true, CrcStartByte = 0, CrcEndByte = 0, CrcDataStartByte = 1,
        CrcDataIdEnabled = true, CrcDataIdMode = mode, CrcProfile1DataIdHex = "0123",
        CrcDataIdTable = Enumerable.Range(1, 16).Select(i => i.ToString("X2")).ToArray()
    };
    private static (IExecutionContext Context, PayloadExpressionContext State) Context(CanCrcCalculateSetting setting)
    {
        var context = DispatchProxy.Create<IExecutionContext, PayloadExpressionContext>();
        var state = (PayloadExpressionContext)(object)context; state.Plugin = new CanCrcCalculatePlugin(); state.Setting = setting;
        return (context, state);
    }
}
