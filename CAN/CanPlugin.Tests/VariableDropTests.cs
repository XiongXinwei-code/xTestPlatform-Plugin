using System.Reflection;
using System.IO;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml.Linq;
using Xunit;

namespace CAN.Tests;

public sealed class VariableDropTests
{
    public sealed class Target
    {
        public string Value { get; set; } = "Locals.Old";
        public string Other { get; set; } = "unrelated";
    }

    private static Type Behavior => typeof(CAN.UI.Dbc.DbcEditorView).Assembly.GetType("PluginCommon.UI.VariableDrop")!;

    [StaFact]
    public void Dropping_a_variable_replaces_the_path_and_updates_the_binding_immediately()
    {
        var target = new Target();
        var box = new TextBox { DataContext = target };
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(Target.Value)) { Mode = BindingMode.TwoWay });
        var behavior = Behavior;
        Assert.NotNull(behavior);
        behavior.GetMethod("SetTarget")!.Invoke(null, [box, true]);
        var args = Drop(box, "FileGlobals.Measurement");
        Assert.True(args.Handled);
        Assert.Equal("FileGlobals.Measurement", target.Value);
    }

    [StaFact]
    public void Disabled_readonly_and_multiline_targets_do_not_modify_the_binding()
    {
        var target = new Target();
        var box = new TextBox { DataContext = target };
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(Target.Value)) { Mode = BindingMode.TwoWay });
        Behavior.GetMethod("SetTarget")!.Invoke(null, [box, true]);
        Assert.Equal(DragDropEffects.None, Drop(box, "one\ntwo").Effects);
        box.IsReadOnly = true;
        Assert.Equal(DragDropEffects.None, Drop(box, "Locals.New").Effects);
        box.IsReadOnly = false;
        box.IsEnabled = false;
        Assert.Equal(DragDropEffects.None, Drop(box, "Locals.New").Effects);
        Assert.Equal("Locals.Old", target.Value);
    }

    [StaFact]
    public void DataGrid_drop_updates_only_the_selected_variable_column_and_row()
    {
        var first = new Target();
        var second = new Target();
        var rows = new ObservableCollection<Target> { first, second };
        var grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, CanUserAddRows = false };
        var valueColumn = new DataGridTextColumn { Header = "Variable", Binding = new Binding(nameof(Target.Value)) };
        var otherColumn = new DataGridTextColumn { Header = "Path", Binding = new Binding(nameof(Target.Other)) };
        grid.Columns.Add(valueColumn);
        grid.Columns.Add(otherColumn);
        Behavior.GetMethod("SetColumns")!.Invoke(null, [grid, nameof(Target.Value)]);
        var window = new Window { Content = grid, Width = 400, Height = 200, ShowInTaskbar = false, ShowActivated = false,
            WindowStyle = WindowStyle.ToolWindow, Left = -2000, Top = -2000 };
        try
        {
            window.Show();
            window.UpdateLayout();
            grid.ScrollIntoView(second, valueColumn);
            window.UpdateLayout();
            var content = valueColumn.GetCellContent(second);
            Assert.NotNull(content);
            Assert.True(((UIElement)content).AllowDrop);
            var result = Drop(content, "Locals.Second");
            Assert.True(result.Handled);
            Assert.True(result.Effects == DragDropEffects.Copy,
                $"Effect={result.Effects}, rows={rows.Count}, current={grid.CurrentItem?.GetType().FullName}, editing={grid.CurrentCell.IsValid}, count={grid.Items.Count}");
            Assert.Equal("Locals.Second", second.Value);
            Assert.Equal("Locals.Old", first.Value);
            var other = otherColumn.GetCellContent(second);
            Assert.NotNull(other);
            var rejected = Drop(other, "Locals.Wrong");
            Assert.True(rejected.Handled);
            Assert.Equal(DragDropEffects.None, rejected.Effects);
            Assert.Equal("unrelated", second.Other);
        }
        finally { window.Close(); }
    }

    [StaFact]
    public void Dropping_on_a_new_item_creates_and_commits_the_mapped_row()
    {
        var rows = new ObservableCollection<Target>();
        var grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, CanUserAddRows = true };
        var column = new DataGridTextColumn { Header = "Variable", Binding = new Binding(nameof(Target.Value)) };
        grid.Columns.Add(column);
        Behavior.GetMethod("SetColumns")!.Invoke(null, [grid, nameof(Target.Value)]);
        int began = 0, ended = 0;
        grid.BeginningEdit += (_, _) => began++;
        grid.CellEditEnding += (_, _) => ended++;
        var window = new Window { Content = grid, Width = 400, Height = 200, ShowInTaskbar = false, ShowActivated = false,
            WindowStyle = WindowStyle.ToolWindow, Left = -2000, Top = -2000 };
        try
        {
            window.Show();
            window.UpdateLayout();
            var placeholder = CollectionView.NewItemPlaceholder;
            grid.ScrollIntoView(placeholder, column);
            window.UpdateLayout();
            var content = column.GetCellContent(placeholder);
            Assert.NotNull(content);
            DependencyObject? node = content;
            while (node != null && node is not DataGridCell) node = VisualTreeHelper.GetParent(node);
            var cell = Assert.IsType<DataGridCell>(node);
            Assert.False(cell.IsReadOnly);
            Assert.True(cell.IsEnabled);
            var rowContainer = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(placeholder);
            Assert.Same(placeholder, rowContainer.Item);
            var result = Drop(content, "Locals.Created");
            Assert.True(result.Effects == DragDropEffects.Copy,
                $"Effect={result.Effects}, rows={rows.Count}, current={grid.CurrentItem?.GetType().FullName}, editing={grid.CurrentCell.IsValid}, count={grid.Items.Count}, began={began}, ended={ended}");
            Assert.Equal("Locals.Created", Assert.Single(rows).Value);
        }
        finally { window.Close(); }
    }

    [StaFact]
    public void A_cancelled_edit_does_not_report_a_successful_drop()
    {
        var item = new Target();
        var rows = new ObservableCollection<Target> { item };
        var grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, CanUserAddRows = false };
        var column = new DataGridTextColumn { Header = "Variable", Binding = new Binding(nameof(Target.Value)) };
        grid.Columns.Add(column);
        Behavior.GetMethod("SetColumns")!.Invoke(null, [grid, nameof(Target.Value)]);
        grid.CellEditEnding += (_, e) => e.Cancel = true;
        var window = new Window { Content = grid, Width = 400, Height = 200, ShowInTaskbar = false, ShowActivated = false,
            WindowStyle = WindowStyle.ToolWindow, Left = -2000, Top = -2000 };
        try
        {
            window.Show();
            window.UpdateLayout();
            var result = Drop(column.GetCellContent(item), "Locals.Rejected");
            Assert.Equal(DragDropEffects.None, result.Effects);
            Assert.Equal("Locals.Old", item.Value);
        }
        finally { window.Close(); }
    }

    [StaFact]
    public void Rejected_new_row_drop_does_not_leave_an_empty_mapping()
    {
        var rows = new ObservableCollection<Target>();
        var grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, CanUserAddRows = true };
        var column = new DataGridTextColumn { Header = "Variable", Binding = new Binding(nameof(Target.Value)) };
        grid.Columns.Add(column);
        Behavior.GetMethod("SetColumns")!.Invoke(null, [grid, nameof(Target.Value)]);
        grid.CellEditEnding += (_, e) => { if (e.EditAction == DataGridEditAction.Commit) e.Cancel = true; };
        var window = new Window { Content = grid, Width = 400, Height = 200, ShowInTaskbar = false, ShowActivated = false,
            WindowStyle = WindowStyle.ToolWindow, Left = -2000, Top = -2000 };
        try
        {
            window.Show();
            window.UpdateLayout();
            var placeholder = CollectionView.NewItemPlaceholder;
            grid.ScrollIntoView(placeholder, column);
            window.UpdateLayout();
            var result = Drop(column.GetCellContent(placeholder), "Locals.Rejected");
            Assert.Equal(DragDropEffects.None, result.Effects);
            Assert.Empty(rows);
        }
        finally { window.Close(); }
    }

    private static DragEventArgs Drop(UIElement target, string text)
    {
        var data = new DataObject(DataFormats.StringFormat, text);
        var constructor = typeof(DragEventArgs).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects), typeof(DependencyObject), typeof(Point)], null)!;
        var args = (DragEventArgs)constructor.Invoke([data, DragDropKeyStates.LeftMouseButton, DragDropEffects.Copy, target, new Point()]);
        args.RoutedEvent = UIElement.PreviewDropEvent;
        target.RaiseEvent(args);
        return args;
    }

    [Fact]
    public void All_xaml_variable_paths_are_explicitly_marked_for_drop()
    {
        string root = RepositoryRoot();
        string[] plugins = ["CAN", "CiA402", "Ethernet", "Http", "LIN", "Modbus", "NiDaq", "NiGrpc", "OpcUa", "SerialPort", "VISA"];
        XNamespace drop = "clr-namespace:PluginCommon.UI";
        foreach (string plugin in plugins)
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, plugin), "*.xaml", SearchOption.AllDirectories)
                     .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin")))
        {
            var doc = XDocument.Load(path);
            foreach (var element in doc.Descendants().Where(x => x.Name.LocalName is "TextBox" or "DataGridTextColumn"))
            {
                string? binding = (string?)element.Attribute(element.Name.LocalName == "TextBox" ? "Text" : "Binding");
                var match = System.Text.RegularExpressions.Regex.Match(binding ?? "", @"^\{Binding\s+(\w+)");
                if (!match.Success || !match.Groups[1].Value.EndsWith("Variable", StringComparison.Ordinal)) continue;
                string field = match.Groups[1].Value;
                if (path.EndsWith(Path.Combine("Dbc", "DbcEditorView.xaml"), StringComparison.Ordinal))
                {
                    Assert.True(field == "ResultVariable" ? element.Attribute("PreviewDrop") != null :
                        element.Ancestors().Any(x => x.Name.LocalName == "DataGrid" && x.Attribute("PreviewMouseLeftButtonDown") != null));
                }
                else if (element.Name.LocalName == "TextBox")
                    Assert.Equal("True", (string?)element.Attribute(drop + "VariableDrop.Target"));
                else
                {
                    var grid = element.Ancestors().First(x => x.Name.LocalName == "DataGrid");
                    Assert.Contains(field, ((string?)grid.Attribute(drop + "VariableDrop.Columns") ?? "").Split(','));
                }
            }
        }
    }

    private static string RepositoryRoot([CallerFilePath] string file = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", ".."));
}
