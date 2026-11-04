using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Dbc;

public partial class DbcEditorView : UserControl, IRefreshableEditor
{
    public DbcEditorViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(DbcEditorView));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(DbcEditorView));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }
    public DbcEditorView(IStepPlugin plugin) : this(new DbcEditorViewModel(plugin)) { }
    internal DbcEditorView(DbcEditorViewModel model) : this(model, DbcUiDispatch.For(System.Windows.Threading.Dispatcher.CurrentDispatcher)) { }
    internal DbcEditorView(DbcEditorViewModel model, DbcPostBatch postBatch)
    {
        _postBatch = postBatch;
        ViewModel = model;
        SignalPresenter = new(model, postBatch);
        InitializeComponent(); DataContext = ViewModel;
        SizeChanged += (_, _) => QueueNextStage();
        Loaded += OnViewLoaded;
        PluginCommon.UI.PluginEditorCommit.Attach(this, ViewModel.Flush);
        Unloaded += OnViewUnloaded;
    }
    public void RefreshFromStep(Step step)
    {
        if (_unloaded) { _pendingStep = step; return; }
        if (ViewModel.IsOwnPublishedRefresh(step)) return;
        RestartPendingBuild();
        ShowPageNotice(null);
        ViewModel.Attach(step);
        QueueNextStage();
    }
    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProtectionTab?.IsSelected != true || ProtectionHost == null || ProtectionHost.Content != null) return;
        CreateEditor(ProtectionHost, "ProtectionEditorTemplate", "ProtectionEditor");
    }

    private void CreateEditor(ContentControl host, string templateKey, string controlName)
    {
        var template = (DataTemplate)Resources[templateKey];
        var editor = (FrameworkElement)template.LoadContent();
        host.Content = editor;
        RegisterName(controlName, editor);
    }

    private void OnSignalBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is DbcSignalRow row && IsSignalEditor(e.Column) && !CanEditSignal(e.Column, row)) e.Cancel = true;
    }

    private void OnSignalPreparingEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (!IsSignalEditor(e.Column)) return;
        e.EditingElement.ApplyTemplate();
        e.EditingElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    private bool IsSignalEditor(DataGridColumn column) =>
        column == ValueColumn || column == ValueKindColumn || column == OutputVariableColumn || column == OutputValueKindColumn;

    private bool CanEditSignal(DataGridColumn column, DbcSignalRow row) =>
        column == ValueColumn || column == ValueKindColumn ? row.IsEditable : row.CanBindOutput;

    private void OnKindEditorLoaded(object sender, RoutedEventArgs e)
    {
        var choices = (ComboBox)sender;
        choices.Focus(); choices.IsDropDownOpen = true;
    }

    private void OnSignalMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Keep the familiar single click on an enabled expression while ordinary
        // rows need only a text presenter during scrolling and initial layout.
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source != null && source is not DataGridCell)
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        if (source is not DataGridCell { IsEditing: false, DataContext: DbcSignalRow row } cell ||
            !IsSignalEditor(cell.Column) || !CanEditSignal(cell.Column, row)) return;
        cell.Focus();
        SignalsGrid.CurrentCell = new DataGridCellInfo(row, cell.Column);
        if (SignalsGrid.BeginEdit(e)) e.Handled = true;
    }

    private static bool TryGetDraggedText(IDataObject data, out string value)
    {
        value = data.GetDataPresent(DataFormats.StringFormat)
            ? data.GetData(DataFormats.StringFormat) as string ?? "" : "";
        return !string.IsNullOrWhiteSpace(value) && !value.Contains('\r') && !value.Contains('\n');
    }

    private void OnVariablePathDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDraggedText(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnResultVariableDrop(object sender, DragEventArgs e)
    {
        if (sender is not TextBox box || !TryGetDraggedText(e.Data, out string value)) return;
        box.SetCurrentValue(TextBox.TextProperty, value);
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnSignalDragOver(object sender, DragEventArgs e)
    {
        if (sender is not DataGridCell cell || cell.DataContext is not DbcSignalRow row ||
            (cell.Column != ValueColumn && cell.Column != OutputVariableColumn)) return;
        // An active expression editor owns caret insertion and its native drop handling.
        if (cell.Column == ValueColumn && cell.IsEditing) return;
        e.Effects = CanEditSignal(cell.Column, row) && TryGetDraggedText(e.Data, out _)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnSignalDrop(object sender, DragEventArgs e)
    {
        if (sender is not DataGridCell cell || cell.DataContext is not DbcSignalRow row ||
            (cell.Column != ValueColumn && cell.Column != OutputVariableColumn)) return;
        if (cell.Column == ValueColumn && cell.IsEditing) return;
        if (!CanEditSignal(cell.Column, row) || !TryGetDraggedText(e.Data, out string value))
        {
            e.Handled = true;
            return;
        }
        if (!CommitSignalCell()) { e.Handled = true; return; }
        if (!ViewModel.SignalRows.Contains(row)) { e.Handled = true; return; }
        if (cell.Column == ValueColumn) row.Value = value;
        else row.OutputVariable = value;
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }
    private async void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "CAN 数据库 (*.dbc)|*.dbc|所有文件 (*.*)|*.*", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog() == true) await ViewModel.LoadFileAsync(dialog.FileName);
    }
    private void OnBrowsePath(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "CAN 数据库 (*.dbc)|*.dbc|所有文件 (*.*)|*.*", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog() == true) ViewModel.DbcFilePath = dialog.FileName;
    }
    private async void OnReload(object sender, RoutedEventArgs e) => await ViewModel.ReloadSourceAsync();
    private void OnAddMessage(object sender, RoutedEventArgs e) => ViewModel.AddSelectedMessage();
    private void OnRemoveMessage(object sender, RoutedEventArgs e) => ViewModel.RemoveMessages(MessagesGrid.SelectedItems.Cast<DbcConfiguredMessageRow>().ToArray());
    private void OnBeginBatchAdd(object sender, RoutedEventArgs e) => ViewModel.BeginMessageSelection();
    private void OnSelectVisibleMessages(object sender, RoutedEventArgs e) => ViewModel.BatchSelection?.SelectVisible(true);
    private void OnDeselectVisibleMessages(object sender, RoutedEventArgs e) => ViewModel.BatchSelection?.SelectVisible(false);
    private void OnConfirmBatchAdd(object sender, RoutedEventArgs e) => ViewModel.ApplyMessageSelection((sender as FrameworkElement)?.DataContext as DbcBatchMessageSelection);
    private void OnCancelBatchAdd(object sender, RoutedEventArgs e) => ViewModel.CancelBatchSelection();
    private void OnResetReadSignals(object sender, RoutedEventArgs e) => ViewModel.ResetReadSignalSettings();

    private void OnSignalPageKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        JumpToEnteredPage(); e.Handled = true;
    }

    private void OnSignalPageNavigation(object sender, RoutedEventArgs e)
    {
        int current = ViewModel.SignalPageNumber;
        if (sender == SignalJumpPage) { JumpToEnteredPage(); return; }
        int destination = sender == SignalFirstPage ? 1 : sender == SignalLastPage ? ViewModel.SignalPageCount :
            sender == SignalPreviousPage ? current - 1 : current + 1;
        ChangeSignalPage(destination);
    }

    private void JumpToEnteredPage()
    {
        if (!int.TryParse(SignalPageInput.Text, out int destination) || destination < 1 || destination > ViewModel.SignalPageCount)
        {
            ShowPageNotice($"请输入 1 到 {ViewModel.SignalPageCount} 之间的页码。");
            return;
        }
        ChangeSignalPage(destination);
    }

    private void ChangeSignalPage(int destination)
    {
        if (!CommitSignalCell()) return;
        ViewModel.GoToSignalPage(destination);
        SignalPageInput.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        ShowPageNotice(null);
        // The new page begins at the top. ScrollIntoView(first) synchronously realizes
        // and lays out the group again inside the navigation click.
        FindScrollViewer(SignalsGrid)?.ScrollToTop();
    }

    private void OnLocateSignalMessage(object sender, RoutedEventArgs e)
    {
        if (SignalLocateSelector.SelectedItem is not DbcConfiguredMessageRow selected)
        {
            ShowPageNotice("请先在定位列表中选择已配置的报文。");
            return;
        }
        if (!CommitSignalCell() || !ViewModel.ConfiguredMessages.Contains(selected)) return;
        ViewModel.LocateSignalMessage(selected);
        if (!ViewModel.ConfiguredMessages.Contains(selected)) return;
        var first = ViewModel.SignalRows.FirstOrDefault(row => row.MessageId == selected.Item.MessageId &&
            row.IsExtended == (selected.Item.FrameType == CAN.Models.CanFrameType.Extended));
        ShowPageNotice(first == null ? "所选报文在当前 DBC 中没有可显示信号。" : null);
        if (first != null)
        {
            if (SignalsGrid.Visibility != Visibility.Visible)
            {
                SignalRegion.LayoutUpdated -= CheckSignalViewport;
                SignalsGrid.Visibility = Visibility.Visible;
                SignalRegion.BringIntoView();
            }
            // BringIntoView raises a routed event. Its handler may change the step
            // or detach this view before the requested row is ready to scroll.
            if (!IsCurrentTarget(first)) return;
            SignalPresenter.BringRequestedRow(first, row =>
            {
                if (IsCurrentTarget(row)) SignalsGrid.ScrollIntoView(row);
            });
            bool IsCurrentTarget(DbcSignalRow row) => !_unloaded &&
                ViewModel.ConfiguredMessages.Contains(selected) && ViewModel.SignalRows.Contains(row);
        }
    }

    private bool CommitSignalCell()
    {
        var current = SignalsGrid.CurrentCell;
        if (current.IsValid && current.Column.GetCellContent(current.Item) is { } content)
        {
            for (DependencyObject? node = content; node is Visual; node = VisualTreeHelper.GetParent(node))
                if (node is DataGridCell cell)
                {
                    if (!cell.IsEditing || SignalsGrid.CommitEdit(DataGridEditingUnit.Cell, true)) return true;
                    ShowPageNotice("当前信号的编辑值未能提交，请先修正后再翻页。");
                    return false;
                }
        }
        return true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer viewer) return viewer;
            if (FindScrollViewer(child) is { } descendant) return descendant;
        }
        return null;
    }

    private void ShowPageNotice(string? text)
    {
        if (FindName(nameof(SignalPageNotice)) == null) return;
        SignalPageNotice.Text = text ?? "";
        SignalPageNotice.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ObserveSignalRegion()
    {
        ViewModel.SignalRows.CollectionChanged -= SignalCountChanged;
        if (_unloaded) return;
        ViewModel.SignalRows.CollectionChanged += SignalCountChanged;
        SyncSignalRegion();
    }

    private void SignalCountChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => SyncSignalRegion();

    private void SyncSignalRegion()
    {
        SignalRegion.LayoutUpdated -= CheckSignalViewport;
        if (_unloaded) return;
        if (ViewModel.SignalRows.Count == 0)
        {
            SignalPresenter.Deactivate();
            SignalsGrid.Visibility = SignalRegion.Visibility = Visibility.Collapsed;
            return;
        }
        // Keep the scroll extent stable before the grid first enters the viewport.
        int groups = ViewModel.SignalRows.Select(row => row.MessageGroup).Distinct().Count();
        SignalRegion.MinHeight = Math.Clamp(36d * ViewModel.SignalRows.Count + 32 + groups * 27, 180, SignalsGrid.MaxHeight);
        SignalRegion.Visibility = Visibility.Visible;
        if (SignalsGrid.Visibility != Visibility.Visible) SignalRegion.LayoutUpdated += CheckSignalViewport;
    }

    private void CheckSignalViewport(object? sender, EventArgs e)
    {
        if (_unloaded || SignalRegion.ActualHeight <= 0 || EditorScroll.ViewportHeight <= 0 ||
            !EditorScroll.IsAncestorOf(SignalRegion)) return;
        double top = SignalRegion.TranslatePoint(new Point(), EditorScroll).Y;
        if (top >= EditorScroll.ViewportHeight || top + SignalRegion.ActualHeight <= 0) return;
        // Keep scroll extent while off screen. Realize the themed cells once,
        // when the user reaches this region, then retain them during editing.
        SignalRegion.LayoutUpdated -= CheckSignalViewport;
        SignalsGrid.Visibility = Visibility.Visible;
        if (!_unloaded) SignalPresenter.Activate();
    }
}
