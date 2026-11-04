using System.Windows;
using System.Windows.Controls;
using CAN.Models;
using CAN.UI;
using CAN.UI.Views;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class CanPayloadProtectionEditorTests
{
    // Real Syncfusion views must execute on the same STA as every other view in
    // EditorRuntimeTests; the SDK caches dispatcher-owned theme resources.
    internal static void VerifyActualProtectionPages()
    {
        Write_editor_controls_persist_nested_rc_and_custom_crc_and_flush_before_switching_step();
        Cyclic_editor_configures_selected_rows_independently_and_unsubscribes_removed_rows();
        Presets_show_execution_parameters_without_silently_moving_crc_output();
        Data_ID_controls_are_shared_by_read_and_write_and_follow_both_enable_switches();
    }

    private static void Write_editor_controls_persist_nested_rc_and_custom_crc_and_flush_before_switching_step()
    {
        var plugin = new CanWritePlugin();
        var first = Step(plugin, new CanWriteSetting { Data = "\"00 00 00 00 00 00 00 00\"" });
        var second = Step(plugin, new CanWriteSetting());
        var view = new CanWriteEditorView(); view.RefreshFromStep(first);
        Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(view.Content).SelectedIndex = 1;
        var editor = Assert.IsType<PayloadProtectionEditorView>(view.FindName("ProtectionEditor"));
        Layout(view);
        Assert.IsType<CheckBox>(editor.FindName("RcEnabledCheckBox")).IsChecked = true;
        Assert.IsType<TextBox>(editor.FindName("RcInitialValueBox")).Text = "7";
        Assert.IsType<CheckBox>(editor.FindName("CrcEnabledCheckBox")).IsChecked = true;
        Assert.IsType<ComboBox>(editor.FindName("CrcAlgorithmBox")).SelectedIndex = (int)CanCrcAlgorithm.Custom;
        Assert.IsType<TextBox>(editor.FindName("CrcPolynomialBox")).Text = "1D";
        SetDataId(editor, "12 34", CanCrcDataIdPlacement.AfterData);
        var old = view.ViewModel.Protection!;
        view.RefreshFromStep(second); // Attach/refresh must flush pending edits to first.
        var stored = (CanWriteSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1);
        Assert.True(stored.Protection.RcEnabled);
        Assert.Equal(7u, stored.Protection.RcInitialValue);
        Assert.True(stored.Protection.CrcEnabled);
        Assert.Equal(CanCrcAlgorithm.Custom, stored.Protection.CrcAlgorithm);
        Assert.Equal("1D", stored.Protection.CrcPolynomial);
        Assert.True(stored.Protection.CrcDataIdEnabled);
        Assert.Equal("12 34", stored.Protection.CrcDataIdHex);
        Assert.Equal(CanCrcDataIdPlacement.AfterData, stored.Protection.CrcDataIdPlacement);
        var untouched = second.StepSetting.Setting.ToArray();
        old.RcInitialValue = 99;
        old.CrcDataIdHex = "FF";
        view.ViewModel.Flush();
        Assert.Equal(untouched, second.StepSetting.Setting);
        Assert.False(view.ViewModel.Protection!.RcEnabled);
        Assert.False(view.ViewModel.Protection.CrcDataIdEnabled);
        Assert.Equal(string.Empty, view.ViewModel.Protection.CrcDataIdHex);
        view.RefreshFromStep(first);
        Assert.Equal(7u, view.ViewModel.Protection!.RcInitialValue);
        Assert.Equal("12 34", view.ViewModel.Protection.CrcDataIdHex);
        Assert.Equal(CanCrcDataIdPlacement.AfterData, view.ViewModel.Protection.CrcDataIdPlacement);
    }

    private static void Cyclic_editor_configures_selected_rows_independently_and_unsubscribes_removed_rows()
    {
        var plugin = new CanCyclicSendStartPlugin();
        var step = Step(plugin, new CanCyclicSendStartSetting { Messages = [new(), new()] });
        var view = new CanCyclicSendStartEditorView(); view.RefreshFromStep(step); Layout(view);
        var grid = Assert.IsType<DataGrid>(view.FindName("MessagesGrid"));
        var editor = Assert.IsType<PayloadProtectionEditorView>(view.FindName("ProtectionEditor"));
        var first = view.ViewModel.Messages[0]; var second = view.ViewModel.Messages[1];
        grid.SelectedItem = first;
        Assert.IsType<CheckBox>(editor.FindName("RcEnabledCheckBox")).IsChecked = true;
        Assert.IsType<TextBox>(editor.FindName("RcInitialValueBox")).Text = "3";
        Assert.IsType<CheckBox>(editor.FindName("CrcEnabledCheckBox")).IsChecked = true;
        SetDataId(editor, "01 02", CanCrcDataIdPlacement.BeforeData);
        grid.SelectedItem = second;
        Assert.IsType<CheckBox>(editor.FindName("RcEnabledCheckBox")).IsChecked = true;
        Assert.IsType<TextBox>(editor.FindName("RcInitialValueBox")).Text = "9";
        Assert.IsType<CheckBox>(editor.FindName("CrcEnabledCheckBox")).IsChecked = true;
        SetDataId(editor, "A0 B0", CanCrcDataIdPlacement.AfterData);
        view.ViewModel.Flush();
        var stored = (CanCyclicSendStartSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1);
        Assert.Equal(3u, stored.Messages[0].Protection.RcInitialValue);
        Assert.Equal(9u, stored.Messages[1].Protection.RcInitialValue);
        Assert.Equal("01 02", stored.Messages[0].Protection.CrcDataIdHex);
        Assert.Equal(CanCrcDataIdPlacement.BeforeData, stored.Messages[0].Protection.CrcDataIdPlacement);
        Assert.Equal("A0 B0", stored.Messages[1].Protection.CrcDataIdHex);
        Assert.Equal(CanCrcDataIdPlacement.AfterData, stored.Messages[1].Protection.CrcDataIdPlacement);
        Assert.All(stored.Messages, row => Assert.True(row.Protection.CrcDataIdEnabled));
        view.ViewModel.RemoveMessage(second); view.ViewModel.Flush();
        var saved = step.StepSetting.Setting.ToArray();
        second.Protection.RcInitialValue = 42;
        second.Protection.CrcDataIdHex = "FF";
        view.ViewModel.Flush();
        Assert.Equal(saved, step.StepSetting.Setting);
        Assert.Same(first, view.ViewModel.SelectedMessage);
        view.RefreshFromStep(step);
        Assert.Equal(3u, Assert.Single(view.ViewModel.Messages).Protection.RcInitialValue);
        Assert.Equal("01 02", Assert.Single(view.ViewModel.Messages).Protection.CrcDataIdHex);
    }

    private static void SetDataId(PayloadProtectionEditorView editor, string hex, CanCrcDataIdPlacement placement)
    {
        Assert.IsType<CheckBox>(editor.FindName("CrcDataIdEnabledCheckBox")).IsChecked = true;
        Assert.IsType<TextBox>(editor.FindName("CrcDataIdHexBox")).Text = hex;
        Assert.IsType<ComboBox>(editor.FindName("CrcDataIdPlacementBox")).SelectedIndex = (int)placement;
    }

    private static void Data_ID_controls_are_shared_by_read_and_write_and_follow_both_enable_switches()
    {
        foreach (bool readMode in new[] { false, true })
        {
            var setting = new CanPayloadProtectionSetting();
            var view = new PayloadProtectionEditorView { DataContext = setting, ReadMode = readMode }; Layout(view);
            var enabled = Assert.IsType<CheckBox>(view.FindName("CrcDataIdEnabledCheckBox"));
            var input = Assert.IsType<TextBox>(view.FindName("CrcDataIdHexBox"));
            var placement = Assert.IsType<ComboBox>(view.FindName("CrcDataIdPlacementBox"));
            Assert.Equal(Visibility.Visible, enabled.Visibility);
            Assert.False(enabled.IsEnabled); Assert.False(input.IsEnabled); Assert.False(placement.IsEnabled);
            Assert.IsType<CheckBox>(view.FindName("CrcEnabledCheckBox")).IsChecked = true;
            Assert.True(enabled.IsEnabled); Assert.False(input.IsEnabled);
            SetDataId(view, "34 12", CanCrcDataIdPlacement.BeforeData);
            Assert.True(input.IsEnabled); Assert.True(placement.IsEnabled);
            Assert.Equal("34 12", setting.CrcDataIdHex);
            enabled.IsChecked = false;
            Assert.False(input.IsEnabled); Assert.False(placement.IsEnabled);
            Assert.Equal("34 12", setting.CrcDataIdHex); // Disabling keeps the user's configuration.
            enabled.IsChecked = true;
            Assert.IsType<CheckBox>(view.FindName("CrcEnabledCheckBox")).IsChecked = false;
            Assert.False(enabled.IsEnabled); Assert.False(input.IsEnabled); Assert.False(placement.IsEnabled);
            CaptureDataId(readMode);
        }
    }

    private static void CaptureDataId(bool readMode)
    {
        var output = Environment.GetEnvironmentVariable("CAN_DATA_ID_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(output)) return;
        var view = new PayloadProtectionEditorView { ReadMode = readMode, DataContext = new CanPayloadProtectionSetting
        { CrcEnabled = true, CrcDataIdEnabled = true, CrcDataIdHex = "34 12" } };
        var scroll = new ScrollViewer { Content = view, Background = System.Windows.Media.Brushes.White,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.Measure(new Size(900, 620)); scroll.Arrange(new Rect(0, 0, 900, 620)); scroll.UpdateLayout();
        scroll.ScrollToBottom(); scroll.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(900, 620, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(scroll);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        System.IO.Directory.CreateDirectory(output);
        using var file = System.IO.File.Create(System.IO.Path.Combine(output, readMode ? "data-id-read.png" : "data-id-write.png"));
        encoder.Save(file);
    }

    private static void Presets_show_execution_parameters_without_silently_moving_crc_output()
    {
        var setting = new CanPayloadProtectionSetting { CrcEnabled = true };
        var view = new PayloadProtectionEditorView { DataContext = setting }; Layout(view);
        var algorithms = Assert.IsType<ComboBox>(view.FindName("CrcAlgorithmBox"));
        Assert.Equal(Enum.GetValues<CanCrcAlgorithm>().Length, algorithms.Items.Count);
        algorithms.SelectedIndex = (int)CanCrcAlgorithm.Crc64Ecma182;
        Assert.Equal(CanCrcAlgorithm.Crc64Ecma182, setting.CrcAlgorithm);
        var summary = Assert.IsType<TextBlock>(view.FindName("CrcSummaryText")).Text;
        Assert.Contains("64", summary); Assert.Contains("8 个输出字节", summary);
        Assert.Contains("42F0E1EBA9EA3693", summary);
        Assert.Equal(7, setting.CrcStartByte); Assert.Equal(7, setting.CrcEndByte);
        algorithms.SelectedIndex = (int)CanCrcAlgorithm.Custom;
        setting.CrcPolynomial = "not hex";
        Assert.Contains("无效", Assert.IsType<TextBlock>(view.FindName("CrcSummaryText")).Text);
    }

    [Fact]
    public async Task Both_editors_reject_overlapping_output_and_literal_payload_bounds()
    {
        var protection = new CanPayloadProtectionSetting
        {
            RcEnabled = true, CrcEnabled = true, RcStartByte = 7, RcEndByte = 7
        };
        var write = new CanWriteSetting { Data = "\"00 00 00 00 00 00 00 00\"", Protection = protection };
        var errors = await new CanWriteEditorPlugin().ValidateWithContextAsync(
            Round2XcpContractTests.Context(new CanWritePlugin(), write));
        Assert.Contains(errors, e => e.Code == "CAN_PAYLOAD_PROTECTION");
        var cyclic = new CanCyclicSendStartSetting { Messages = [new() { Data = "\"00\"", Protection = new() { CrcEnabled = true } }] };
        errors = await new CanCyclicSendStartEditorPlugin().ValidateWithContextAsync(
            Round2XcpContractTests.Context(new CanCyclicSendStartPlugin(), cyclic));
        Assert.Contains(errors, e => e.Code == "CAN_PAYLOAD_PROTECTION" && e.Message.Contains("第 1 行"));
    }

    [Fact]
    public async Task Dynamic_payload_keeps_runtime_length_check_while_crc_configuration_is_still_validated()
    {
        var setting = new CanWriteSetting { Data = "Locals.Payload", UseFdFrame = true,
            Protection = new() { CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Custom, CrcPolynomial = "invalid" } };
        var errors = await new CanWriteEditorPlugin().ValidateWithContextAsync(
            Round2XcpContractTests.Context(new CanWritePlugin(), setting));
        Assert.Contains(errors, e => e.Code == "CAN_PAYLOAD_PROTECTION");
        setting.Protection.CrcPolynomial = "07";
        errors = await new CanWriteEditorPlugin().ValidateWithContextAsync(
            Round2XcpContractTests.Context(new CanWritePlugin(), setting));
        Assert.DoesNotContain(errors, e => e.Code == "CAN_PAYLOAD_PROTECTION");
    }

    [Theory]
    [InlineData("")]
    [InlineData("GG")]
    [InlineData("0x")]
    [InlineData("123")]
    public async Task Both_editors_reject_invalid_enabled_Data_ID_but_allow_disabled_configuration(string dataId)
    {
        var protection = new CanPayloadProtectionSetting
        {
            CrcEnabled = true, CrcDataIdEnabled = true, CrcDataIdHex = dataId
        };
        var write = new CanWriteSetting { Data = "Locals.Payload", Protection = protection };
        var cyclic = new CanCyclicSendStartSetting { Messages = [new() { Data = "Locals.Payload", Protection = protection }] };
        async Task<IReadOnlyList<StepSettingError>> WriteErrors() => await new CanWriteEditorPlugin().ValidateWithContextAsync(
            Round2XcpContractTests.Context(new CanWritePlugin(), write));
        async Task<IReadOnlyList<StepSettingError>> CyclicErrors() => await new CanCyclicSendStartEditorPlugin().ValidateWithContextAsync(
            Round2XcpContractTests.Context(new CanCyclicSendStartPlugin(), cyclic));
        Assert.Contains(await WriteErrors(), e => e.Code == "CAN_PAYLOAD_PROTECTION" && e.Message.Contains("Data ID"));
        Assert.Contains(await CyclicErrors(), e => e.Code == "CAN_PAYLOAD_PROTECTION" && e.Message.Contains("Data ID") && e.Message.Contains("第 1 行"));
        protection.CrcDataIdEnabled = false;
        Assert.DoesNotContain(await WriteErrors(), e => e.Code == "CAN_PAYLOAD_PROTECTION");
        Assert.DoesNotContain(await CyclicErrors(), e => e.Code == "CAN_PAYLOAD_PROTECTION");
        protection.CrcDataIdEnabled = true; protection.CrcEnabled = false;
        Assert.DoesNotContain(await WriteErrors(), e => e.Code == "CAN_PAYLOAD_PROTECTION");
        Assert.DoesNotContain(await CyclicErrors(), e => e.Code == "CAN_PAYLOAD_PROTECTION");
    }

    private static Step Step(IStepPlugin plugin, object setting)
    {
        var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion;
        return step;
    }

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1000, 700)); view.Arrange(new Rect(0, 0, 1000, 700)); view.UpdateLayout();
    }
}
