using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Dbc;

public partial class DbcEditorView
{
    private readonly DbcPostBatch _postBatch;
    private CancellationTokenSource _stageLifetime = new();
    private TaskCompletionSource _controlsReady = NewCompletion();
    private bool _stageQueued, _unloaded, _mountingOptional, _watchingShutdown;
    private Step? _pendingStep;

    internal DbcSignalPagePresenter SignalPresenter { get; }
    internal int CompletedStageCount { get; private set; }
    internal Task ControlsReady => _controlsReady.Task;

    private static TaskCompletionSource NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        if (Dispatcher.HasShutdownStarted) { SuspendView(); return; }
        if (!_watchingShutdown)
        {
            Dispatcher.ShutdownStarted += OnDispatcherShutdown;
            _watchingShutdown = true;
        }
        if (_unloaded)
        {
            _unloaded = false;
            RestartPendingBuild();
        }
        if (_pendingStep is { } pending)
        {
            _pendingStep = null;
            ViewModel.Attach(pending);
        }
        else ViewModel.Activate();
        if (CompletedStageCount >= 1) ObserveOptionalSections();
        if (CompletedStageCount == 3) ObserveSignalRegion();
        QueueNextStage();
    }

    private void OnViewUnloaded(object sender, RoutedEventArgs e) => SuspendView();
    private void OnDispatcherShutdown(object? sender, EventArgs e) => SuspendView();

    private void SuspendView()
    {
        if (_watchingShutdown)
        {
            Dispatcher.ShutdownStarted -= OnDispatcherShutdown;
            _watchingShutdown = false;
        }
        if (_unloaded) return;
        _unloaded = true;
        try
        {
            if (CompletedStageCount < 3) _controlsReady.TrySetCanceled();
            RenewLifetime();
            ViewModel.PropertyChanged -= OptionalSectionChanged;
            ViewModel.SignalRows.CollectionChanged -= SignalCountChanged;
            // Content assignment can synchronously unload the view while stage 3
            // has registered these names but has not incremented the stage count.
            if (FindName(nameof(SignalRegion)) is Grid region)
            {
                region.LayoutUpdated -= CheckSignalViewport;
                if (FindName(nameof(SignalsGrid)) is DataGrid grid) grid.Visibility = Visibility.Collapsed;
                region.Visibility = Visibility.Collapsed;
            }
        }
        finally
        {
            try { SignalPresenter.Deactivate(); }
            finally { ViewModel.Deactivate(); }
        }
    }

    private void RenewLifetime()
    {
        var old = _stageLifetime;
        _stageLifetime = new();
        _stageQueued = false;
        old.Cancel();
        old.Dispose();
    }

    private void RestartPendingBuild()
    {
        if (CompletedStageCount == 3 && _controlsReady.Task.IsCompletedSuccessfully) return;
        RenewLifetime();
        _controlsReady.TrySetCanceled();
        _controlsReady = NewCompletion();
        StageErrorText.Text = "";
        StageFailure.Visibility = Visibility.Collapsed;
    }

    private void QueueNextStage()
    {
        if (_stageQueued || _unloaded || _controlsReady.Task.IsCompleted ||
            ActualWidth <= 0 || ActualHeight <= 0) return;
        // A real first layout exposes the source fields and the host's TabControlExt.
        // Posting only the next batch lets Input/Render run again between sections.
        var token = _stageLifetime.Token;
        _stageQueued = true;
        try
        {
            _postBatch(() => BuildNextStage(token), token);
        }
        catch (Exception error)
        {
            if (token.IsCancellationRequested || _unloaded) return;
            _stageQueued = false;
            ReportStageFailure(error);
        }
    }

    private void BuildNextStage(CancellationToken token)
    {
        if (token.IsCancellationRequested || _unloaded || _controlsReady.Task.IsCompleted) return;
        _stageQueued = false;
        try
        {
            switch (CompletedStageCount)
            {
                case 0: CreateMessageStage(); break;
                case 1: CreateSettingsStage(); break;
                case 2: CreateSignalStage(); break;
            }
            if (CompletedStageCount < 3) CompletedStageCount++;
            if (token.IsCancellationRequested || _unloaded) return;
            MountOptionalSections();
            if (token.IsCancellationRequested || _unloaded) return;
            if (CompletedStageCount == 3) _controlsReady.TrySetResult();
            else QueueNextStage();
        }
        catch (Exception error)
        {
            if (!token.IsCancellationRequested) ReportStageFailure(error);
        }
    }

    private void ReportStageFailure(Exception error)
    {
        if (_controlsReady.Task.IsCompleted) _controlsReady = NewCompletion();
        _controlsReady.TrySetException(error);
        _ = _controlsReady.Task.Exception;
        StageErrorText.Text = $"DBC 设置区域未完成加载：{error.Message}";
        StageFailure.Visibility = Visibility.Visible;
    }

    private void OnRetryStages(object sender, RoutedEventArgs e)
    {
        RestartPendingBuild();
        QueueNextStage();
    }

    // LoadContent creates a template-local namescope. Register intentional public part
    // names on the view as well: host integrations and UI tests use view.FindName().
    private void Mount(ContentControl host, string key, string names, Action? configure = null)
    {
        if (Resources[key] is not DataTemplate template)
            throw new InvalidOperationException($"未找到 DBC 编辑器模板 {key}");
        var root = (FrameworkElement)template.LoadContent();
        var parts = names.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => (Name: name, Value: root.Name == name ? root : root.FindName(name)
                ?? throw new InvalidOperationException($"DBC 编辑器模板 {key} 缺少控件 {name}"))).ToArray();
        object? previous = host.Content;
        int registered = 0;
        try
        {
            foreach (var part in parts) { RegisterName(part.Name, part.Value); registered++; }
            host.Content = root;
            configure?.Invoke();
        }
        catch
        {
            host.Content = previous;
            for (int i = 0; i < registered; i++) UnregisterName(parts[i].Name);
            throw;
        }
    }

    private void CreateMessageStage()
    {
        Mount(MessageStageHost, "MessageStageTemplate",
            "UseMessageListCheckBox BatchAddButton AddMessageSelector RemoveMessagesButton BatchSelectionPanel BatchPickerHost " +
            "MessagesGrid AutomaticCycleColumn ManualCycleColumn EffectiveCycleColumn", () =>
            {
                if (!ViewModel.IsCyclic) HideColumns("AutomaticCycleColumn", "ManualCycleColumn", "EffectiveCycleColumn");
            });
        ObserveOptionalSections();
    }

    private void CreateSettingsStage() => Mount(SettingsStageHost, "SettingsStageTemplate",
        "MessageSelector ConfiguredMessageSelector TimeoutBox TaskNameHost UseDbcCycleTimeCheckBox CycleTimeBox " +
        "CycleSourceText ResultVariableBox PublishIntervalBox MaxAgeBox", () =>
        {
            if (ViewModel.HasTaskName) CreateEditor(Part<ContentControl>("TaskNameHost"), "TaskNameTemplate", "TaskNameEditor");
        });

    private void CreateSignalStage() => Mount(SignalStageHost, "SignalStageTemplate",
        "SignalSummaryText SignalPager SignalFirstPage SignalPreviousPage SignalPageInput SignalJumpPage SignalNextPage " +
        "SignalLastPage SignalLocateSelector SignalLocateButton SignalPageNotice SignalRegion SignalsGrid " +
        "SelectedColumn OutputSelectedColumn ValueColumn ValueKindColumn OutputVariableColumn OutputValueKindColumn DefaultValueColumn " +
        "SignalPresentationStatus SignalPresentationRetry", () =>
        {
            if (!ViewModel.IsWrite) HideColumns("SelectedColumn", "ValueColumn", "ValueKindColumn", "DefaultValueColumn");
            if (!ViewModel.IsRead) HideColumns("OutputSelectedColumn", "OutputVariableColumn", "OutputValueKindColumn");
            Part<FrameworkElement>("SignalPresentationStatus").DataContext = SignalPresenter;
            Part<FrameworkElement>("SignalPresentationRetry").DataContext = SignalPresenter;
            SignalsGrid.ItemsSource = SignalPresenter.ItemsView;
            ObserveSignalRegion();
        });

    private void OnRetryPresentation(object sender, RoutedEventArgs e)
    {
        if (CommitSignalCell()) SignalPresenter.Retry();
    }

    private void HideColumns(params string[] names)
    {
        foreach (string name in names) Part<DataGridColumn>(name).Visibility = Visibility.Collapsed;
    }

    private void ObserveOptionalSections()
    {
        ViewModel.PropertyChanged -= OptionalSectionChanged;
        if (!_unloaded) ViewModel.PropertyChanged += OptionalSectionChanged;
    }

    private void OptionalSectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(ViewModel.HasLoadIssues) or nameof(ViewModel.HasBatchSelection))
        {
            if (_unloaded || _controlsReady.Task.IsFaulted) return;
            try { MountOptionalSections(); }
            catch (Exception error) { ReportStageFailure(error); }
        }
    }

    private void MountOptionalSections()
    {
        if (_unloaded || CompletedStageCount < 1 || _mountingOptional) return;
        _mountingOptional = true;
        try
        {
            if (ViewModel.HasLoadIssues && IssueGridHost.Content == null)
                Mount(IssueGridHost, "IssueGridTemplate", "LoadIssuesGrid");
            if (!_unloaded && ViewModel.HasBatchSelection && Part<ContentControl>("BatchPickerHost") is { Content: null } host)
                Mount(host, "BatchPickerTemplate", "BatchFilterBox BatchSelectVisibleButton BatchConfirmButton BatchMessagesGrid BatchSummaryText", () =>
                {
                    // The picker is instantiated on demand, before its ContentPresenter
                    // is laid out. Bind to the owning model, independent of host layout.
                    if (host.Content is not FrameworkElement picker)
                        throw new InvalidOperationException("DBC 报文选择模板没有生成可绑定的界面。");
                    picker.SetBinding(DataContextProperty, new Binding(nameof(ViewModel.BatchSelection)) { Source = ViewModel });
                });
        }
        finally { _mountingOptional = false; }
    }

    private T Part<T>(string name) where T : class => (T)FindName(name);
    private DataGrid MessagesGrid => Part<DataGrid>(nameof(MessagesGrid));
    private DataGrid SignalsGrid => Part<DataGrid>(nameof(SignalsGrid));
    private Grid SignalRegion => Part<Grid>(nameof(SignalRegion));
    private TextBox SignalPageInput => Part<TextBox>(nameof(SignalPageInput));
    private TextBlock SignalPageNotice => Part<TextBlock>(nameof(SignalPageNotice));
    private ComboBox SignalLocateSelector => Part<ComboBox>(nameof(SignalLocateSelector));
    private Button SignalFirstPage => Part<Button>(nameof(SignalFirstPage));
    private Button SignalPreviousPage => Part<Button>(nameof(SignalPreviousPage));
    private Button SignalJumpPage => Part<Button>(nameof(SignalJumpPage));
    private Button SignalLastPage => Part<Button>(nameof(SignalLastPage));
    private DataGridColumn ValueColumn => Part<DataGridColumn>(nameof(ValueColumn));
    private DataGridColumn ValueKindColumn => Part<DataGridColumn>(nameof(ValueKindColumn));
    private DataGridColumn OutputVariableColumn => Part<DataGridColumn>(nameof(OutputVariableColumn));
    private DataGridColumn OutputValueKindColumn => Part<DataGridColumn>(nameof(OutputValueKindColumn));
}
