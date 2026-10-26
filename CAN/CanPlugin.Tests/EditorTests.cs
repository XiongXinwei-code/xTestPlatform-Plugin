using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CAN.CANopen.Cyclic;
using CAN.UI.Views;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class EditorTests
{
    private static Step Step(CyclicPluginBase plugin)
    {
        var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = plugin.CreateSerializer().Serialize(new CyclicSetting());
        return step;
    }

    internal void RouteGridEditsPersistAndRemovedRowsCannotChangeOtherSteps()
    {
        var plugin = new ServiceStartPlugin(); var first = Step(plugin); var second = Step(plugin);
        var view = new CyclicServiceEditorView(plugin);
        view.RefreshFromStep(first);
        view.ViewModel.AddRoute();
        view.Measure(new Size(700, 500)); view.Arrange(new Rect(0, 0, 700, 500)); view.UpdateLayout();
        var grid = Assert.IsType<DataGrid>(view.FindName("RoutesGrid"));
        var row = view.ViewModel.Routes[0];
        grid.SelectedItem = row; grid.CurrentCell = new(row, grid.Columns[1]); grid.ScrollIntoView(row); grid.UpdateLayout();
        Assert.True(grid.BeginEdit());
        var presenter = grid.Columns[1].GetCellContent(row);
        var editor = Descendants(presenter).Single(c => c.GetType().FullName == "ExpressionTextBox.ExpressionTextBox");
        editor.GetType().GetProperty("ScriptText")!.SetValue(editor, "0x281");
        var scriptProperty = (DependencyProperty)editor.GetType().GetField("ScriptTextProperty")!.GetValue(null)!;
        BindingOperations.GetBindingExpression(editor, scriptProperty)!.UpdateSource();
        Assert.True(grid.CommitEdit(DataGridEditingUnit.Cell, true));
        view.ViewModel.Flush();
        var saved = (CyclicSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1);
        Assert.Equal("0x281", Assert.Single(saved.FeedbackRoutes).CobId);
        Assert.False(grid.CanUserSortColumns);
        view.ViewModel.RemoveRoute(row);
        view.RefreshFromStep(second);
        var before = second.StepSetting.Setting.ToArray(); row.CobId = "0x481";
        Assert.Equal(before, second.StepSetting.Setting);
        var hint = Assert.IsType<TextBlock>(view.FindName("ServiceHint"));
        Assert.True(hint.ActualHeight > 0);
        Assert.Equal(TextWrapping.Wrap, hint.TextWrapping);
    }

    internal void AllSixEditorsLoadAndLayoutWithIndependentStepTypes()
    {
        CyclicPluginBase[] plugins = [new ServiceStartPlugin(), new CycleStartPlugin(), new CycleInputsUpdatePlugin(),
            new SnapshotReadPlugin(), new CycleStopPlugin(), new ServiceStopPlugin()];
        Assert.Equal(6, plugins.Select(p => p.StepTypeId).Distinct().Count());
        foreach (var plugin in plugins)
        {
            var view = new CyclicServiceEditorView(plugin); var step = Step(plugin);
            view.RefreshFromStep(step);
            view.Measure(new Size(640, 450)); view.Arrange(new Rect(0, 0, 640, 450)); view.UpdateLayout();
            view.ViewModel.ServiceName = "\"Changed\"";
            view.ViewModel.Flush();
            var saved = (CyclicSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1);
            Assert.Equal("\"Changed\"", saved.ServiceName);
            Assert.True(view.ActualHeight > 0);
            var tabs = Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(view.Content);
            var tab = Assert.IsType<Syncfusion.Windows.Tools.Controls.TabItemExt>(Assert.Single(tabs.Items.Cast<object>()));
            Assert.Equal(plugin.DisplayName, tab.Header);
            Assert.NotNull(tab.Image);
            Assert.NotNull(view.GetType().GetProperty("SequenceFile"));
            Assert.NotNull(view.GetType().GetProperty("EditPosition"));
            if (plugin.Operation == CyclicOperation.ServiceStop)
            {
                var hint = Assert.IsType<TextBlock>(view.FindName("ServiceStopResultHint"));
                Assert.Equal(TextWrapping.Wrap, hint.TextWrapping);
                Assert.Contains("历史故障", hint.Text);
            }
        }
    }

    internal void TargetBytesAndOrderSurviveSaveReload()
    {
        var plugin = new CycleStartPlugin(); var step = Step(plugin); var view = new CyclicServiceEditorView(plugin);
        view.RefreshFromStep(step);
        view.ViewModel.AddTarget(); view.ViewModel.AddTarget();
        view.ViewModel.Targets[0].Data = "Locals.TargetBytes";
        view.ViewModel.Targets[1].CobId = "0x301";
        view.ViewModel.PeriodMs = 25;
        view.RefreshFromStep(step);
        Assert.Equal("Locals.TargetBytes", view.ViewModel.Targets[0].Data);
        Assert.Equal("0x301", view.ViewModel.Targets[1].CobId);
        Assert.Equal(25, view.ViewModel.PeriodMs);
        Assert.True(view.ViewModel.RequireOnBusConfirmation);
    }

    internal void NewMonitoringTrajectoryAndStopFieldsPersistInActualControls()
    {
        var monitorPlugin = new ServiceStartPlugin(); var monitorStep = Step(monitorPlugin);
        var monitorView = new CyclicServiceEditorView(monitorPlugin); monitorView.RefreshFromStep(monitorStep);
        var timingHint = Assert.IsType<TextBlock>(monitorView.FindName("MonitoringTimingHint"));
        Assert.Equal(TextWrapping.Wrap, timingHint.TextWrapping);
        Assert.Contains("发送确认期限", timingHint.Text);
        Assert.False(monitorView.ViewModel.EnableMonitoringSync);
        monitorView.ViewModel.EnableMonitoringSync = true; monitorView.ViewModel.MonitoringPeriodMs = 20;
        monitorView.ViewModel.SyncData = "\"00\"";
        monitorView.RefreshFromStep(monitorStep);
        Assert.True(monitorView.ViewModel.EnableMonitoringSync);
        Assert.Equal(20, monitorView.ViewModel.MonitoringPeriodMs);
        monitorView.Measure(new Size(640, 450)); monitorView.Arrange(new Rect(0, 0, 640, 450)); monitorView.UpdateLayout();
        var startupCleanup = Assert.IsType<TextBox>(monitorView.FindName("StopTimeoutMsEditor"));
        Assert.Equal("5000", startupCleanup.Text);
        startupCleanup.Text = "2345";
        monitorView.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal(2345, ((CyclicSetting)monitorPlugin.CreateSerializer().Deserialize(monitorStep.StepSetting.Setting, 1)).StopTimeoutMs);
        var plugin = new CycleStartPlugin(); var step = Step(plugin); var view = new CyclicServiceEditorView(plugin);
        view.RefreshFromStep(step);
        view.Measure(new Size(640, 450)); view.Arrange(new Rect(0, 0, 640, 450)); view.UpdateLayout();
        var input = Assert.IsAssignableFrom<DependencyObject>(view.FindName("TrajectoryJsonEditor"));
        input.GetType().GetProperty("ScriptText")!.SetValue(input, "Locals.PointsJson");
        var scriptProperty = (DependencyProperty)input.GetType().GetField("ScriptTextProperty")!.GetValue(null)!;
        BindingOperations.GetBindingExpression(input, scriptProperty)!.UpdateSource();
        view.ViewModel.StopCommandsJson = "Locals.ReviewedStopJson";
        view.ViewModel.UseFiniteTrajectory = true; view.ViewModel.SuperviseFeedback = true;
        view.Measure(new Size(640, 450)); view.Arrange(new Rect(0, 0, 640, 450)); view.UpdateLayout();
        view.RefreshFromStep(step);
        Assert.Equal("Locals.PointsJson", view.ViewModel.TrajectoryJson);
        Assert.Equal("Locals.ReviewedStopJson", view.ViewModel.StopCommandsJson);
        Assert.True(view.ViewModel.UseFiniteTrajectory); Assert.True(view.ViewModel.SuperviseFeedback);
        var tabs = Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(view.Content);
        var tab = Assert.IsType<Syncfusion.Windows.Tools.Controls.TabItemExt>(tabs.Items[0]);
        Assert.True(Assert.IsType<ScrollViewer>(tab.Content).ScrollableHeight > 0);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(System.Windows.Media.VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
