using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CAN.Models;
using CAN.UI.Views;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

internal static class Round2UnloadEditorTests
{
    internal static void VerifyActualViews()
    {
        var failures = new List<string>();
        foreach (var plugin in new IStepPlugin[] { new CanOpenPlugin(), new CanClosePlugin() })
        {
            var serializer = plugin.CreateSerializer(); var step = new Step();
            step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault()); step.StepSetting.SettingVersion = 1;
            UserControl view = plugin is CanOpenPlugin ? new CanOpenEditorView() : new CanCloseEditorView();
            view.GetType().GetMethod("RefreshFromStep")!.Invoke(view, [step]); Layout(view);
            var input = Logical(view).OfType<TextBox>().Single(t => BindingOperations.GetBinding(t, TextBox.TextProperty)?.Path.Path == "TimeoutMs");
            input.Text = "2345";
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            object saved = serializer.Deserialize(step.StepSetting.Setting, 1);
            if (!Equals(2345, saved.GetType().GetProperty("TimeoutMs")!.GetValue(saved))) failures.Add(plugin.StepTypeId + " 卸载没有保存");
        }
        var cyclePlugin = new CanCyclicSendStartPlugin(); var cycleSerializer = cyclePlugin.CreateSerializer(); var cycleStep = new Step();
        cycleStep.StepSetting.Setting = cycleSerializer.Serialize(new CanCyclicSendStartSetting { Messages = [new() { CanId = "\"0x101\"" }, new() { CanId = "\"0x202\"" }] }); cycleStep.StepSetting.SettingVersion = 1;
        var cycle = new CanCyclicSendStartEditorView(); cycle.RefreshFromStep(cycleStep); Layout(cycle);
        var grid = Logical(cycle).OfType<DataGrid>().Single(); grid.SelectedItem = cycle.ViewModel.Messages[0];
        var remove = Logical(cycle).OfType<Button>().Single(b => Equals(b.Content, "Remove"));
        remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); cycle.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var rows = ((CanCyclicSendStartSetting)cycleSerializer.Deserialize(cycleStep.StepSetting.Setting, 1)).Messages;
        if (rows.Count != 1 || rows[0].CanId != "\"0x202\"") failures.Add("周期发送删除了未选中的行");
        Assert.True(failures.Count == 0, string.Join("；", failures));
    }
    private static void Layout(UserControl view) { view.Measure(new Size(900, 800)); view.Arrange(new Rect(0, 0, 900, 800)); view.UpdateLayout(); }
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach (var nested in Logical(child)) yield return nested;
    }
}
