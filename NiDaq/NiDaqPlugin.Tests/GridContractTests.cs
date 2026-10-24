using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using NiDaq.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class GridContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ActualRemoveButtonDeletesSelectedRowAndPreservesUnselectedRow(int list) => UiThread.Run(() =>
    {
        var (plugin, page, step, grid, root) = Create(list);
        var first = grid.Items[0]; var second = grid.Items[1]; grid.SelectedItem = first;
        var remove = Logical(root).OfType<Button>().Single(b => Equals(b.Content, "删除通道"));
        remove.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Single(grid.Items); Assert.Same(second, grid.Items[0]);
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var saved = plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        var channels = list == 0 ? ((NiDaqAiConfigSetting)saved).Channels.Cast<object>().ToArray()
            : list == 1 ? ((NiDaqSyncConfigSetting)saved).AiChannels.Cast<object>().ToArray() : ((NiDaqSyncConfigSetting)saved).EncoderChannels.Cast<object>().ToArray();
        Assert.Single(channels); Assert.Equal("B", channels[0].GetType().GetProperty("ColumnName")!.GetValue(channels[0]));
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ActualEditingTextIsCommittedBeforeUnloadAndRefresh(int list) => UiThread.Run(() =>
    {
        var (plugin, page, step, grid, _) = Create(list);
        var item = grid.Items[0]; var column = grid.Columns.OfType<DataGridTextColumn>().Single(c => ((Binding)c.Binding).Path.Path == "ColumnName");
        grid.SelectedItem = item; grid.CurrentCell = new DataGridCellInfo(item, column); grid.ScrollIntoView(item, column); grid.UpdateLayout();
        Assert.True(grid.BeginEdit()); grid.UpdateLayout();
        var text = Assert.IsType<TextBox>(column.GetCellContent(item)); text.Text = "EditingWithoutLostFocus";
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        ((IRefreshableEditor)page).RefreshFromStep(step);
        var saved = plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        var first = list == 0 ? (object)((NiDaqAiConfigSetting)saved).Channels[0]
            : list == 1 ? ((NiDaqSyncConfigSetting)saved).AiChannels[0] : ((NiDaqSyncConfigSetting)saved).EncoderChannels[0];
        Assert.Equal("EditingWithoutLostFocus", first.GetType().GetProperty("ColumnName")!.GetValue(first));
    });

    [Fact]
    public void ActualSyncZIndexColumnCanBeEditedAndSerialized() => UiThread.Run(() =>
    {
        var (plugin, page, step, grid, _) = Create(2);
        var column = Assert.Single(grid.Columns.OfType<DataGridCheckBoxColumn>(), c => c.Binding is Binding b && b.Path.Path == "ZIndexEnable");
        var item = grid.Items[0]; grid.SelectedItem = item; grid.CurrentCell = new DataGridCellInfo(item, column); grid.ScrollIntoView(item, column); grid.UpdateLayout();
        Assert.True(grid.BeginEdit()); grid.UpdateLayout();
        var checkbox = Assert.IsType<CheckBox>(column.GetCellContent(item)); checkbox.IsChecked = true;
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var saved = (NiDaqSyncConfigSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.True(saved.EncoderChannels[0].ZIndexEnable);
    });

    private static (IStepPlugin Plugin, UserControl Page, Step Step, DataGrid Grid, object Root) Create(int list)
    {
        IStepPlugin plugin = list == 0 ? new NiDaqAiConfigPlugin() : new NiDaqSyncConfigPlugin();
        object setting = list == 0 ? new NiDaqAiConfigSetting { Channels = [new() { PhysicalChannel = "Dev1/ai0", ColumnName = "A" }, new() { PhysicalChannel = "Dev1/ai1", ColumnName = "B" }] }
            : new NiDaqSyncConfigSetting { AiChannels = [new() { PhysicalChannel = "Dev1/ai0", ColumnName = "A" }, new() { PhysicalChannel = "Dev1/ai1", ColumnName = "B" }],
                EncoderChannels = [new() { CounterChannel = "Dev1/ctr0", ColumnName = "A" }, new() { CounterChannel = "Dev1/ctr1", ColumnName = "B" }] };
        IStepEditorPlugin editor = list == 0 ? new NiDaq.UI.NiDaqAiConfigEditorPlugin() : new NiDaq.UI.NiDaqSyncConfigEditorPlugin();
        var step = new Step(); var serializer = plugin.CreateSerializer(); step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var page = (UserControl)editor.CreateEditor(step, new SequenceFile()); var tabs = page.Content;
        int selected = list == 2 ? 4 : 3; tabs.GetType().GetProperty("SelectedIndex")!.SetValue(tabs, selected);
        var items = ((IEnumerable)tabs.GetType().GetProperty("Items")!.GetValue(tabs)!).Cast<object>().ToArray();
        var root = items[selected].GetType().GetProperty("Content")!.GetValue(items[selected])!;
        page.Measure(new Size(1200, 800)); page.Arrange(new Rect(0, 0, 1200, 800)); page.UpdateLayout();
        page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        var grid = Logical(root).OfType<DataGrid>().Single(); grid.UpdateLayout();
        return (plugin, page, step, grid, root);
    }
    private static IEnumerable<object> Logical(object parent)
    {
        yield return parent;
        if (parent is not DependencyObject dependency) yield break;
        foreach (var child in LogicalTreeHelper.GetChildren(dependency)) foreach (var nested in Logical(child)) yield return nested;
    }
}
