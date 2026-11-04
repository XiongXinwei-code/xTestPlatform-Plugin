using System.Windows;
using System.Windows.Controls;
using CAN.Models;
using CAN.UI;
using CAN.UI.Views;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class CanMultiWriteEditorTests
{
    // Called by the existing shared STA view fixture; do not create a second
    // Syncfusion Dispatcher or pump its deferred license dialog in this test.
    internal static void VerifyActualMultiWritePages()
    {
        var plugin = new CanWritePlugin();
        var first = Step(plugin, new CanWriteSetting { Data = "\"AA BB\"", UseMessageList = true,
            Messages = [new() { CanId = "\"0x101\"" }, new() { CanId = "\"0x202\"" }] });
        var next = Step(plugin, new CanWriteSetting());
        var view = new CanWriteEditorView(); view.RefreshFromStep(first);
        Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(view.Content).SelectedIndex = 2;
        Layout(view);
        var grid = Assert.IsType<DataGrid>(view.FindName("MessagesGrid"));
        var protection = Assert.IsType<PayloadProtectionEditorView>(view.FindName("BatchProtectionEditor"));
        Assert.Contains(grid.Columns, column => Equals(column.Header, "RC"));
        Assert.Contains(grid.Columns, column => Equals(column.Header, "CRC"));
        var a = view.ViewModel.Messages[0]; var b = view.ViewModel.Messages[1];
        grid.SelectedItem = a;
        Assert.Contains("0x101", Assert.IsType<TextBlock>(view.FindName("BatchProtectionTitle")).Text);
        Assert.IsType<CheckBox>(protection.FindName("RcEnabledCheckBox")).IsChecked = true;
        Assert.IsType<TextBox>(protection.FindName("RcInitialValueBox")).Text = "3";
        grid.SelectedItem = b;
        Assert.Contains("0x202", Assert.IsType<TextBlock>(view.FindName("BatchProtectionTitle")).Text);
        Assert.IsType<CheckBox>(protection.FindName("CrcEnabledCheckBox")).IsChecked = true;
        Assert.IsType<ComboBox>(protection.FindName("CrcAlgorithmBox")).SelectedIndex = (int)CanCrcAlgorithm.Crc16Modbus;
        view.RefreshFromStep(next);
        var saved = (CanWriteSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1);
        Assert.True(saved.UseMessageList); Assert.Equal("\"AA BB\"", saved.Data);
        Assert.True(saved.Messages[0].Protection.RcEnabled); Assert.False(saved.Messages[0].Protection.CrcEnabled);
        Assert.Equal(3u, saved.Messages[0].Protection.RcInitialValue);
        Assert.False(saved.Messages[1].Protection.RcEnabled); Assert.Equal(CanCrcAlgorithm.Crc16Modbus, saved.Messages[1].Protection.CrcAlgorithm);
        var unchanged = next.StepSetting.Setting.ToArray(); a.Protection.RcInitialValue = 55; view.ViewModel.Flush();
        Assert.Equal(unchanged, next.StepSetting.Setting);
        view.RefreshFromStep(first); var detached = view.ViewModel.Messages[1]; view.ViewModel.RemoveMessage(detached); view.ViewModel.Flush();
        var afterRemoval = first.StepSetting.Setting.ToArray(); detached.Protection.CrcEnabled = false; view.ViewModel.Flush();
        Assert.Equal(afterRemoval, first.StepSetting.Setting);
        Assert.Single(view.ViewModel.Messages); Assert.Same(view.ViewModel.Messages[0], view.ViewModel.SelectedMessage);
        view.ViewModel.AddMessage(); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal(2, ((CanWriteSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1)).Messages.Count);

        var cyclicPlugin = new CanCyclicSendStartPlugin();
        var cyclicView = new CanCyclicSendStartEditorView();
        cyclicView.RefreshFromStep(Step(cyclicPlugin, new CanCyclicSendStartSetting { Messages = [new() { CanId = "\"0x303\"" }, new() { CanId = "\"0x404\"" }] }));
        Layout(cyclicView);
        var cyclicGrid = Assert.IsType<DataGrid>(cyclicView.FindName("MessagesGrid"));
        Assert.Contains(cyclicGrid.Columns, column => Equals(column.Header, "RC"));
        Assert.Contains(cyclicGrid.Columns, column => Equals(column.Header, "CRC"));
        cyclicGrid.SelectedItem = cyclicView.ViewModel.Messages[1];
        Assert.Contains("0x404", Assert.IsType<TextBlock>(cyclicView.FindName("SelectedProtectionTitle")).Text);
        cyclicView.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }

    [Fact]
    public async Task Multi_write_validates_enabled_rows_only_and_preserves_old_single_mode()
    {
        var plugin = new CanWritePlugin(); var editor = new CanWriteEditorPlugin();
        var setting = new CanWriteSetting { UseMessageList = true, CanId = "invalid old expression", Data = "invalid old expression", Messages =
        [new() { CanId = "\"0x100\"", Data = "\"00\"" }, new() { Enabled = false, CanId = "", Data = "", FrameType = (CanFrameType)99, Protection = new() { CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Custom, CrcPolynomial = "invalid" } }] };
        var errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.DoesNotContain(errors, e => e.Code is "CAN_021E" or "CAN_W20E" or "CAN_PAYLOAD_PROTECTION" or "CAN_WRITE_FRAME_TYPE");
        setting.Messages[0].Protection = new() { CrcEnabled = true, CrcStartByte = 7, CrcEndByte = 7 };
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "CAN_PAYLOAD_PROTECTION" && e.Message.Contains("第 1 行"));
        setting.UseMessageList = false; setting.CanId = "\"0x200\""; setting.Data = "\"00 00\"";
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.DoesNotContain(errors, e => e.Code == "CAN_PAYLOAD_PROTECTION");
    }

    [Fact]
    public async Task Multi_write_rejects_empty_or_excessive_list_and_unselected_enabled_rows()
    {
        var plugin = new CanWritePlugin(); var editor = new CanWriteEditorPlugin(); var setting = new CanWriteSetting { UseMessageList = true };
        var errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "CAN_WRITE_MESSAGES");
        setting.Messages.Add(new() { Enabled = false });
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "CAN_WRITE_MESSAGES");
        setting.Messages.Clear();
        for (int i = 0; i < 129; i++) setting.Messages.Add(new() { Enabled = false });
        errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "CAN_WRITE_MESSAGES");
    }

    private static Step Step(IStepPlugin plugin, object setting)
    { var step = new Step(); var serializer = plugin.CreateSerializer(); step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion; return step; }
    private static void Layout(FrameworkElement view)
    { view.Measure(new Size(1200, 850)); view.Arrange(new Rect(0, 0, 1200, 850)); view.UpdateLayout(); }
}
