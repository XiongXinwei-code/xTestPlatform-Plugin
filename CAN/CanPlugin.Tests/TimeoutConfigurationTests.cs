using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CAN.Models;
using CAN.UI.ViewModels;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class TimeoutConfigurationTests
{
    internal void Open_and_close_views_persist_configured_total_timeout()
    {
        foreach (var open in new[] { true, false })
        {
            var plugin = open ? (xTestPlatform.Core.Plugins.Contracts.IStepPlugin)new CanOpenPlugin() : new CanClosePlugin();
            var serializer = plugin.CreateSerializer(); var step = new Step();
            step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault()); step.StepSetting.SettingVersion = serializer.SettingVersion;
            var view = open ? (UserControl)new CAN.UI.Views.CanOpenEditorView() : new CAN.UI.Views.CanCloseEditorView();
            var refresh = view.GetType().GetMethod("RefreshFromStep")!; refresh.Invoke(view, [step]);
            view.Measure(new Size(900, 900)); view.Arrange(new Rect(0, 0, 900, 900)); view.UpdateLayout();
            var input = Assert.Single(Logical(view).OfType<TextBox>(), c =>
                BindingOperations.GetBinding(c, TextBox.TextProperty)?.Path.Path == "TimeoutMs");
            input.Text = "1375"; BindingOperations.GetBindingExpression(input, TextBox.TextProperty)!.UpdateSource();
            refresh.Invoke(view, [step]);
            var setting = serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.Equal(1375, setting.GetType().GetProperty("TimeoutMs")!.GetValue(setting));
            input.Text = "0"; BindingOperations.GetBindingExpression(input, TextBox.TextProperty)!.UpdateSource();
            Assert.True(Validation.GetHasError(input));
        }
    }

    internal void Write_view_persists_timeout_and_legacy_cyclic_view_loads_expression_cells()
    {
        var serializer = new CanWritePlugin().CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(new CanWriteSetting()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var view = new CAN.UI.Views.CanWriteEditorView(); view.RefreshFromStep(step);
        view.Measure(new Size(900, 650)); view.Arrange(new Rect(0, 0, 900, 650)); view.UpdateLayout();
        var timeout = Assert.Single(Logical(view).OfType<TextBox>(), c => BindingOperations.GetBinding(c, TextBox.TextProperty)?.Path.Path == "TimeoutMs");
        Assert.Equal("5000", timeout.Text); timeout.Text = "750"; BindingOperations.GetBindingExpression(timeout, TextBox.TextProperty)!.UpdateSource();
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal(750, ((CanWriteSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion)).TimeoutMs);
        var cyclicSerializer = new CanCyclicSendStartPlugin().CreateSerializer();
        step = new Step(); step.StepSetting.Setting = cyclicSerializer.Serialize(new CanCyclicSendStartSetting()); step.StepSetting.SettingVersion = cyclicSerializer.SettingVersion;
        var cyclic = new CAN.UI.Views.CanCyclicSendStartEditorView(); cyclic.RefreshFromStep(step); cyclic.ViewModel.AddMessage();
        cyclic.Measure(new Size(1000, 700)); cyclic.Arrange(new Rect(0, 0, 1000, 700)); cyclic.UpdateLayout();
        var grid = Assert.Single(Logical(cyclic).OfType<DataGrid>());
        Assert.True(grid.Columns.OfType<DataGridTemplateColumn>().Count() >= 2);
        cyclic.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.NotEmpty(((CanCyclicSendStartSetting)cyclicSerializer.Deserialize(step.StepSetting.Setting, cyclicSerializer.SettingVersion)).Messages);
    }

    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var item in Logical(child)) yield return item;
    }
}
