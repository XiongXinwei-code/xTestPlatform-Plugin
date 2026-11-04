using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace PluginCommon.UI;

/// <summary>在明确标记的变量路径输入框和表格列接收平台变量拖放。</summary>
public static class VariableDrop
{
    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target", typeof(bool), typeof(VariableDrop), new PropertyMetadata(false, OnTargetChanged));

    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.RegisterAttached(
        "Columns", typeof(string), typeof(VariableDrop), new PropertyMetadata("", OnColumnsChanged));

    public static bool GetTarget(DependencyObject element) => (bool)element.GetValue(TargetProperty);
    public static void SetTarget(DependencyObject element, bool value) => element.SetValue(TargetProperty, value);
    public static string GetColumns(DependencyObject element) => (string)element.GetValue(ColumnsProperty);
    public static void SetColumns(DependencyObject element, string value) => element.SetValue(ColumnsProperty, value);

    private static void OnTargetChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not TextBox box) throw new InvalidOperationException("变量路径拖放目标必须是 TextBox。");
        if ((bool)e.NewValue)
        {
            box.AllowDrop = true;
            box.PreviewDragOver += OnTextDragOver;
            box.PreviewDrop += OnTextDrop;
        }
        else
        {
            box.PreviewDragOver -= OnTextDragOver;
            box.PreviewDrop -= OnTextDrop;
            box.ClearValue(UIElement.AllowDropProperty);
        }
    }

    private static void OnColumnsChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not DataGrid grid) throw new InvalidOperationException("变量路径列只能配置在 DataGrid 上。");
        bool wasEnabled = !string.IsNullOrWhiteSpace((string)e.OldValue);
        bool isEnabled = !string.IsNullOrWhiteSpace((string)e.NewValue);
        if (wasEnabled == isEnabled) return;
        if (isEnabled)
        {
            grid.AllowDrop = true;
            grid.PreviewDragOver += OnGridDragOver;
            grid.PreviewDrop += OnGridDrop;
        }
        else
        {
            grid.PreviewDragOver -= OnGridDragOver;
            grid.PreviewDrop -= OnGridDrop;
            grid.ClearValue(UIElement.AllowDropProperty);
        }
    }

    private static bool TryGetPath(IDataObject data, out string path)
    {
        path = data.GetDataPresent(DataFormats.StringFormat)
            ? data.GetData(DataFormats.StringFormat) as string ?? "" : "";
        return !string.IsNullOrWhiteSpace(path) && !path.Contains('\r') && !path.Contains('\n');
    }

    private static void OnTextDragOver(object sender, DragEventArgs e)
    {
        e.Effects = sender is TextBox { IsEnabled: true, IsReadOnly: false } && TryGetPath(e.Data, out _)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnTextDrop(object sender, DragEventArgs e)
    {
        if (sender is TextBox { IsEnabled: true, IsReadOnly: false } box && TryGetPath(e.Data, out string path))
        {
            SetText(box, path);
            e.Effects = DragDropEffects.Copy;
        }
        else e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private static void SetText(TextBox box, string path)
    {
        // Preserve bindings and commit LostFocus-based paths before a step switch.
        box.SetCurrentValue(TextBox.TextProperty, path);
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private static DataGridCell? FindCell(DependencyObject? node)
    {
        while (node != null && node is not DataGridCell)
            node = node is Visual or Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        return node as DataGridCell;
    }

    private static DataGridRow? FindRow(DependencyObject? node)
    {
        while (node != null && node is not DataGridRow)
            node = node is Visual or Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        return node as DataGridRow;
    }

    private static bool IsTargetColumn(DataGrid grid, DataGridCell? cell)
    {
        if (cell?.Column is not DataGridTextColumn { IsReadOnly: false, Binding: Binding binding } ||
            grid.IsReadOnly || !grid.IsEnabled || cell.IsReadOnly || !cell.IsEnabled) return false;
        return GetColumns(grid).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(binding.Path?.Path, StringComparer.Ordinal);
    }

    private static bool HasOwnDropHandler(DependencyObject? source, DataGrid grid)
    {
        for (var node = source; node != null && !ReferenceEquals(node, grid);
             node = node is Visual or Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (GetTarget(node) || node.GetType().FullName == "ExpressionTextBox.ExpressionTextBox") return true;
        }
        return false;
    }

    private static void OnGridDragOver(object sender, DragEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        var cell = FindCell(e.OriginalSource as DependencyObject);
        if (cell == null || !IsTargetColumn(grid, cell))
        {
            if (HasOwnDropHandler(e.OriginalSource as DependencyObject, grid)) return;
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        e.Effects = TryGetPath(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnGridDrop(object sender, DragEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        var cell = FindCell(e.OriginalSource as DependencyObject);
        if (cell == null || !IsTargetColumn(grid, cell))
        {
            if (HasOwnDropHandler(e.OriginalSource as DependencyObject, grid)) return;
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        if (TryGetPath(e.Data, out string path))
        {
            object item = FindRow(cell)?.Item ?? cell.DataContext;
            DataGridColumn column = cell.Column;
            object? source = grid.ItemsSource;
            bool newRow = ReferenceEquals(item, CollectionView.NewItemPlaceholder);
            var current = grid.CurrentCell;
            if (current.IsValid && current.Column.GetCellContent(current.Item) is { } content &&
                FindCell(content) is { IsEditing: true } currentCell &&
                !ReferenceEquals(currentCell, cell) && !grid.CommitEdit(DataGridEditingUnit.Cell, true))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
            if (!grid.IsLoaded || !grid.Items.Contains(item) ||
                (!newRow && !ReferenceEquals(cell.DataContext, item)))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
            if (!cell.IsEditing)
            {
                grid.CurrentCell = new DataGridCellInfo(item, column);
                if (!grid.BeginEdit())
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                    return;
                }
            }
            if (newRow) item = cell.DataContext;
            if (grid.IsLoaded && ReferenceEquals(grid.ItemsSource, source) &&
                !ReferenceEquals(item, CollectionView.NewItemPlaceholder) &&
                grid.Items.Contains(item) && ReferenceEquals(cell.DataContext, item) &&
                column.GetCellContent(item) is TextBox editor && ReferenceEquals(editor.DataContext, item))
            {
                string before = editor.Text;
                SetText(editor, path);
                bool stillOwned = grid.IsLoaded && ReferenceEquals(grid.ItemsSource, source) &&
                    grid.Items.Contains(item) && ReferenceEquals(cell.DataContext, item);
                bool committed = stillOwned && grid.CommitEdit(DataGridEditingUnit.Cell, true);
                if (committed && newRow)
                    committed = grid.IsLoaded && ReferenceEquals(grid.ItemsSource, source) &&
                        grid.Items.Contains(item) && grid.CommitEdit(DataGridEditingUnit.Row, true);
                if (!committed && grid.IsLoaded && ReferenceEquals(grid.ItemsSource, source) &&
                    grid.Items.Contains(item) && ReferenceEquals(editor.DataContext, item))
                {
                    if (newRow) grid.CancelEdit(DataGridEditingUnit.Row);
                    else SetText(editor, before);
                }
                e.Effects = committed ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else
            {
                if (newRow && grid.IsLoaded && ReferenceEquals(grid.ItemsSource, source))
                    grid.CancelEdit(DataGridEditingUnit.Row);
                e.Effects = DragDropEffects.None;
            }
        }
        else e.Effects = DragDropEffects.None;
        e.Handled = true;
    }
}
