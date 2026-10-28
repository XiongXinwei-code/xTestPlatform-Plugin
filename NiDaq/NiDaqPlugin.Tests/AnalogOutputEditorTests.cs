using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NiDaq.Models;
using NiDaq.UI.Views;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class AnalogOutputEditorTests
{
    private static void SetExpression(FrameworkElement control, string value)
    {
        var property = (DependencyProperty)control.GetType().GetField("ScriptTextProperty", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.GetValue(null)!;
        control.SetCurrentValue(property, value);
        control.GetBindingExpression(property)!.UpdateSource();
    }
    private static void Layout(UserControl page)
    {
        page.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        page.Measure(new Size(1000, 600)); page.Arrange(new Rect(0, 0, 1000, 600)); page.UpdateLayout();
        page.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert.True(page.IsMeasureValid);
    }
    [Fact]
    public void AoConfigAllFieldsAndNestedChannelsPersistOnImmediateUnloadAndRefresh() => UiThread.Run(() =>
    {
        var plugin = new NiDaqAoConfigPlugin();
        var serializer = plugin.CreateSerializer();
        var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var editor = new NiDaq.UI.NiDaqAoConfigEditorPlugin();
        var page = Assert.IsType<NiDaqAoConfigEditorView>(editor.CreateEditor(step, new SequenceFile()));
        Layout(page);
        SetExpression((FrameworkElement)page.FindName("TaskNameBox"), "\"AOTest\"");
        SetExpression((FrameworkElement)page.FindName("InitialValuesBox"), "new double[] { 1.5 }");
        var timeout = Assert.IsType<TextBox>(page.FindName("LifecycleTimeoutBox")); timeout.Text = "1234"; timeout.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        page.ViewModel.AddChannel();
        page.ViewModel.Channels[0].PhysicalChannel = "Dev1/ao0";
        page.ViewModel.Channels[0].ColumnName = "output";
        page.ViewModel.Channels[0].MinValue = -5; page.ViewModel.Channels[0].MaxValue = 5;
        ((Syncfusion.Windows.Tools.Controls.TabControlExt)page.FindName("BusinessTabs")).SelectedIndex = 1; Layout(page);
        ((ComboBox)page.FindName("StopPolicyBox")).SelectedItem = AoStopPolicy.WriteFinalValues;
        SetExpression((FrameworkElement)page.FindName("FinalValuesBox"), "0.75");
        var cleanup = (TextBox)page.FindName("CleanupTimeoutBox"); cleanup.Text = "2345"; cleanup.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var saved = (NiDaqAoConfigSetting)serializer.Deserialize(step.StepSetting.Setting, 1);
        Assert.Equal("\"AOTest\"", saved.TaskName); Assert.Equal("new double[] { 1.5 }", saved.InitialValues);
        Assert.Equal("0.75", saved.FinalValues); Assert.Equal(AoStopPolicy.WriteFinalValues, saved.StopPolicy);
        Assert.Equal(1234, saved.TimeoutMs); Assert.Equal(2345, saved.CleanupTimeoutMs);
        Assert.Equal("Dev1/ao0", Assert.Single(saved.Channels).PhysicalChannel); Assert.Equal(5, saved.Channels[0].MaxValue);
        page.RefreshFromStep(step); Layout(page);
        Assert.Equal("0.75", page.ViewModel.FinalValues);
        Assert.Null(page.ViewModel.LastSaveError);
    });

    [Fact]
    public void AoWritePagePersistsExpressionsAndTimeoutBeforeDebounce() => UiThread.Run(() =>
    {
        var serializer = new NiDaqAoWriteSinglePlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var page = Assert.IsType<NiDaqAoWriteSingleEditorView>(new NiDaq.UI.NiDaqAoWriteSingleEditorPlugin().CreateEditor(step, null));
        Layout(page);
        SetExpression((FrameworkElement)page.FindName("TaskNameBox"), "\"AOTest\"");
        SetExpression((FrameworkElement)page.FindName("ValuesBox"), "new double[] { 2.5 }");
        var timeout = (TextBox)page.FindName("LifecycleTimeoutBox"); timeout.Text = "777"; timeout.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var saved = (NiDaqAoWriteSingleSetting)serializer.Deserialize(step.StepSetting.Setting, 1);
        Assert.Equal("\"AOTest\"", saved.TaskName); Assert.Equal("new double[] { 2.5 }", saved.Values); Assert.Equal(777, saved.TimeoutMs);
        page.RefreshFromStep(step); Layout(page); Assert.Equal(777, page.ViewModel.TimeoutMs);
    });
}
