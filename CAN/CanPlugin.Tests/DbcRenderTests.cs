using System.Windows;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Reflection;
using CAN.Dbc;
using CAN.UI.Dbc;
using CAN.UI.Views;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

internal static class DbcRenderTests
{
    internal static DependencyObject EditValue(DataGrid grid, DbcSignalRow row, DataGridColumn column, FrameworkElement view)
        => EditCell(grid, row, column, view, "ExpressionTextBox");

    internal static TextBox EditOutput(DataGrid grid, DbcSignalRow row, DataGridColumn column, FrameworkElement view)
        => (TextBox)EditCell(grid, row, column, view, "TextBox");

    internal static ComboBox EditKind(DataGrid grid, DbcSignalRow row, DataGridColumn column, FrameworkElement view)
        => (ComboBox)EditCell(grid, row, column, view, "ComboBox");

    private static DependencyObject EditCell(DataGrid grid, DbcSignalRow row, DataGridColumn column, FrameworkElement view, string editorType)
    {
        grid.ScrollIntoView(row, column);
        view.UpdateLayout();
        var presenter = Assert.IsAssignableFrom<FrameworkElement>(column.GetCellContent(row));
        var display = Descendants(presenter).OfType<TextBlock>().First();
        // Route the normal preview event through the grid. No physical mouse/device action.
        var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Source = display
        };
        grid.RaiseEvent(click); view.UpdateLayout();
        Assert.True(click.Handled, "已启用的表达式应单击进入编辑");
        return Assert.Single(Descendants(column.GetCellContent(row)!), d => d.GetType().Name == editorType);
    }

    internal static void VerifyActualPages()
    {
        var kinds = new IStepPlugin[] { new DbcWritePlugin(), new DbcCyclicWritePlugin(), new DbcReadPlugin(),
            new DbcCapturePlugin(), new DbcReadBackgroundStartPlugin() };
        string dbc = string.Join("\n", Enumerable.Range(0, 4).Select(i => $"BO_ {291+i} Msg{i}: 16 ECU\n" +
            string.Join("\n", Enumerable.Range(0, 16).Select(j => $" SG_ S{j} : {j}|1@1+ (1,0) [0|1] \"\" ECU"))));
        var entries = kinds.Select(plugin =>
        {
            var serializer = plugin.CreateSerializer(); var setting = (DbcCanSetting)serializer.CreateDefault();
            setting.Document = new() { Name = "render.dbc", Text = dbc }; setting.UseMessageList = true;
            setting.Messages = new(Enumerable.Range(291, 4).Select(id => new DbcMessageItem { MessageId = (uint)id }));
            var step = new Step(); step.StepSetting.StepType = plugin.StepTypeId;
            step.StepSetting.SettingVersion = serializer.SettingVersion; step.StepSetting.Setting = serializer.Serialize(setting);
            var model = new DbcEditorViewModel(plugin); model.Attach(step);
            return (plugin, step, model);
        }).ToArray();
        // Pump the model-only load before constructing any licensed controls.
        var dispatcher = Dispatcher.CurrentDispatcher; var frame = new DispatcherFrame();
        var loading = Task.WhenAll(entries.Select(x => x.model.LoadTask));
        if (!loading.IsCompleted)
        {
            _ = loading.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        loading.GetAwaiter().GetResult();
        var evidence = new List<object>();
        foreach (var (plugin, step, model) in entries)
        {
            var view = DbcStageQueue.Create(model);
            try
            {
                var shortPanel = new Size(1300, 400);
                DbcStageQueue.LayoutAndFinish(view, shortPanel);
                Assert.Empty(model.LoadError);
                Assert.Null(view.FindName("ProtectionEditor"));
                Assert.Equal(model.HasTaskName, view.FindName("TaskNameEditor") != null);
                var grid = Assert.IsType<DataGrid>(view.FindName("SignalsGrid"));
                Assert.Equal(Color.FromRgb(0xDF, 0xEE, 0xFA),
                    Assert.IsType<SolidColorBrush>(grid.TryFindResource(SystemColors.HighlightBrushKey)).Color);
                Assert.Equal(Color.FromRgb(0x1F, 0x29, 0x37),
                    Assert.IsType<SolidColorBrush>(grid.TryFindResource(SystemColors.HighlightTextBrushKey)).Color);
                var messages = Assert.IsType<DataGrid>(view.FindName("MessagesGrid"));
                Assert.Equal(Color.FromRgb(0xDF, 0xEE, 0xFA),
                    Assert.IsType<SolidColorBrush>(messages.TryFindResource(SystemColors.HighlightBrushKey)).Color);
                CAN.UI.Dbc.DbcSelectionPalette.Apply(messages, true);
                Assert.False(messages.Resources.Contains(SystemColors.HighlightBrushKey));
                Assert.False(messages.Resources.Contains(SystemColors.HighlightTextBrushKey));
                CAN.UI.Dbc.DbcSelectionPalette.Apply(messages, false);
                Assert.Empty(Descendants(grid).OfType<DataGridRow>());
                var scroller = Assert.IsType<ScrollViewer>(view.FindName("EditorScroll"));
                scroller.ScrollToEnd(); Layout(view);
                grid.SelectedItem = model.SignalRows[0];
                view.UpdateLayout();
                DependencyObject? selectedCell = grid.Columns[0].GetCellContent(model.SignalRows[0]);
                while (selectedCell != null && selectedCell is not DataGridCell) selectedCell = VisualTreeHelper.GetParent(selectedCell);
                var selected = Assert.IsType<DataGridCell>(selectedCell);
                Assert.True(selected.IsSelected);
                var selectedBackground = Assert.IsType<SolidColorBrush>(selected.Background).Color;
                Assert.True(selectedBackground == Color.FromRgb(0xDF, 0xEE, 0xFA) ||
                    selectedBackground == Color.FromRgb(0xED, 0xF4, 0xFA),
                    $"选中单元格背景未采用浅蓝色：{selectedBackground}");
                var text = Descendants(grid.Columns[0].GetCellContent(model.SignalRows[0]))
                    .OfType<TextBlock>().First();
                var foreground = Assert.IsType<SolidColorBrush>(text.Foreground).Color;
                Assert.True(foreground.R < 128 && foreground.G < 128 && foreground.B < 128,
                    $"浅色选中背景下的文字颜色过亮：{foreground}");
                Assert.Equal(64, model.TotalSignalCount); Assert.Equal(50, grid.Items.Count); Assert.Equal(4, grid.Items.Groups!.Count);
                int realized = Descendants(grid).OfType<DataGridRow>().Count();
                Assert.InRange(realized, 1, grid.Items.Count - 1);
                Assert.DoesNotContain(Descendants(grid), v => v.GetType().Name == "ExpressionTextBox");
                if (model.IsWrite)
                {
                    var row = model.SignalRows.First(); row.Selected = true;
                    var column = grid.Columns.Single(c => Equals(c.Header, "发送值表达式"));
                    DropOnCell(grid, row, column, view, "Locals.Dragged");
                    Assert.Equal("Locals.Dragged", row.Value);
                    row.Selected = false;
                    DropOnCell(grid, row, column, view, "Locals.Disabled");
                    Assert.Equal("Locals.Dragged", row.Value);
                    row.Selected = true;
                    var editor = EditValue(grid, row, column, view);
                    editor.GetType().GetProperty("ScriptText")!.SetValue(editor, "Locals.Target");
                    Assert.True(grid.CommitEdit(DataGridEditingUnit.Cell, true));
                    model.Flush();
                    var saved = (DbcCanSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
                    Assert.Equal("Locals.Target", saved.Messages[0].Signals.Single(s => s.Name == "S0").Value);
                    Assert.DoesNotContain(Descendants(grid), v => v.GetType().Name == "ExpressionTextBox");
                }
                if (model.IsRead)
                {
                    var row = model.SignalRows.First();
                    var column = grid.Columns.Single(c => Equals(c.Header, "输出变量（留空不输出）"));
                    DropOnCell(grid, row, column, view, "Locals.DraggedOutput");
                    var other = model.SignalRows.First(r => r.MessageId != row.MessageId);
                    DropOnCell(grid, other, column, view, "Locals.OtherOutput");
                    var result = Assert.IsType<TextBox>(view.FindName("ResultVariableBox"));
                    RaiseDrop(result, "Locals.DraggedResult");
                    model.Flush();
                    var saved = (DbcReadSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
                    Assert.Equal("Locals.DraggedOutput", saved.Messages[0].SignalOutputs.Single(s => s.Name == "S0").TargetVariable);
                    Assert.Equal("Locals.OtherOutput", saved.Messages[1].SignalOutputs.Single(s => s.Name == "S0").TargetVariable);
                    Assert.Equal("Locals.DraggedResult", saved.ResultVariable);
                }
                var tabs = Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(view.Content);
                tabs.SelectedIndex = 1; Layout(view);
                var protection = Assert.IsType<PayloadProtectionEditorView>(view.FindName("ProtectionEditor"));
                Assert.Same(model.Protection, protection.DataContext);
                model.SelectedConfiguredMessage = model.ConfiguredMessages[^1];
                Assert.Same(model.Protection, protection.DataContext);
                tabs.SelectedIndex = 0; tabs.SelectedIndex = 1; Layout(view);
                Assert.Same(protection, view.FindName("ProtectionEditor"));
                evidence.Add(new { plugin.StepTypeId, Signals = model.TotalSignalCount, PageItems = grid.Items.Count, RealizedRows = realized });
                tabs.SelectedIndex = 0;
                model.RemoveMessages(model.ConfiguredMessages.ToArray()); Layout(view);
                Assert.Empty(grid.Items); Assert.Equal(Visibility.Collapsed, grid.Visibility);
                model.MessageToAdd = model.Messages[0]; model.AddSelectedMessage(); Layout(view);
                Assert.Equal(16, grid.Items.Count); Assert.Equal(Visibility.Visible, grid.Visibility);
                model.RemoveMessages(model.ConfiguredMessages.ToArray()); Layout(view);
                Assert.Empty(grid.Items); Assert.Equal(Visibility.Collapsed, grid.Visibility);
            }
            finally { view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
        }
        var snapshotPlugin = new CAN.CANopen.Cyclic.SnapshotReadPlugin();
        var snapshotSerializer = snapshotPlugin.CreateSerializer();
        var snapshotStep = new Step();
        snapshotStep.StepSetting.StepType = snapshotPlugin.StepTypeId;
        snapshotStep.StepSetting.SettingVersion = snapshotSerializer.SettingVersion;
        snapshotStep.StepSetting.Setting = snapshotSerializer.Serialize(snapshotSerializer.CreateDefault());
        var snapshot = new CyclicServiceEditorView(snapshotPlugin);
        try
        {
            snapshot.RefreshFromStep(snapshotStep);
            DbcStageQueue.LayoutOnce(snapshot, new Size(1300, 850));
            var result = Assert.IsType<TextBox>(snapshot.FindName("ResultVariableEditor"));
            Assert.True(result.AllowDrop);
            RaiseDrop(result, "Locals.CanSnapshot");
            snapshot.ViewModel.Flush();
            var saved = (CAN.CANopen.Cyclic.CyclicSetting)snapshotSerializer.Deserialize(snapshotStep.StepSetting.Setting, snapshotSerializer.SettingVersion);
            Assert.Equal("Locals.CanSnapshot", saved.ResultVariable);
        }
        finally { snapshot.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "dbc-render-ui.json"), JsonSerializer.Serialize(evidence));
    }

    private static void DropOnCell(DataGrid grid, DbcSignalRow row, DataGridColumn column, FrameworkElement view, string text)
    {
        grid.ScrollIntoView(row, column);
        view.UpdateLayout();
        DependencyObject node = column.GetCellContent(row);
        while (node is not DataGridCell) node = VisualTreeHelper.GetParent(node);
        RaiseDrop((DataGridCell)node, text);
    }

    private static void RaiseDrop(UIElement target, string text)
    {
        var data = new DataObject(DataFormats.StringFormat, text);
        var constructor = typeof(DragEventArgs).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects), typeof(DependencyObject), typeof(Point)], null)!;
        var args = (DragEventArgs)constructor.Invoke([data, DragDropKeyStates.LeftMouseButton, DragDropEffects.Copy, target, new Point()]);
        args.RoutedEvent = UIElement.PreviewDropEvent;
        target.RaiseEvent(args);
        Assert.True(args.Handled);
    }

    private static void Layout(FrameworkElement view)
    {
        var size = new Size(1300, 850);
        DbcStageQueue.LayoutAndFinish(view, size);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
