using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;

namespace CAN.UI.Dbc;

internal delegate void DbcPostBatch(Action callback, CancellationToken token);

internal static class DbcUiDispatch
{
    internal static DbcPostBatch For(Dispatcher dispatcher) => (callback, token) =>
    {
        token.ThrowIfCancellationRequested();
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            throw new OperationCanceledException("编辑器调度线程正在关闭", token);
        dispatcher.InvokeAsync(callback, DispatcherPriority.Background, token);
    };
}

/// <summary>Dispatcher-owned presentation of one immutable page of row references.
/// Only the VM owns settings and page navigation. The displayed page is never serialized.</summary>
internal sealed class DbcSignalPagePresenter : INotifyPropertyChanged
{
    private sealed class Page(DbcSignalRow[] rows)
    {
        internal readonly DbcSignalRow[] Rows = rows;
        internal readonly CancellationTokenSource Stop = new();
        internal readonly TaskCompletionSource Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal DbcSignalRow? LocateRow;
        internal Action<DbcSignalRow>? Locate;
    }

    private readonly DbcEditorViewModel _model;
    private readonly DbcPostBatch _post;
    private readonly DbcProjectionCollection<DbcSignalRow> _displayed = [];
    private Page? _page;
    private NotifyCollectionChangedEventHandler? _sourceHandler;
    private bool _active;
    private bool _mutatingDisplay, _clearRequested;

    internal DbcSignalPagePresenter(DbcEditorViewModel model, DbcPostBatch post)
    {
        _model = model;
        _post = post;
        ItemsView = CollectionViewSource.GetDefaultView(_displayed);
        ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DbcSignalRow.MessageGroup)));
    }

    internal ICollectionView ItemsView { get; }
    public int PresentedRowCount => _displayed.Count;
    public bool IsPending => _page is { Ready.Task.IsCompleted: false };
    internal Task PresentationReady => _page?.Ready.Task ?? Task.CompletedTask;
    public string Error { get; private set; } = "";
    public string Status => Error.Length != 0 ? $"信号显示失败：{Error}" : IsPending
        ? $"正在加载当前页的 {_page!.Rows.Length} 个信号" : "";
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Activate()
    {
        if (_active) return;
        _active = true;
        // A multicast notification may outlive unsubscribe and reactivation.
        // An old handler must never restart the new activation's presentation.
        NotifyCollectionChangedEventHandler? handler = null;
        handler = (_, _) => { if (_sourceHandler == handler) Restart(); };
        _sourceHandler = handler;
        _model.SignalRows.CollectionChanged += handler;
        Restart();
    }

    internal void Deactivate()
    {
        if (!_active) return;
        _active = false;
        _model.SignalRows.CollectionChanged -= _sourceHandler;
        _sourceHandler = null;
        CancelPage();
        Error = "";
        RequestClear();
        Notify();
    }

    internal void Retry() { if (_active) Restart(); }

    internal void BringRequestedRow(DbcSignalRow row, Action<DbcSignalRow> bring)
    {
        Activate();
        if (_page is not { } page || !page.Rows.Contains(row) || Error.Length != 0) return;
        page.LocateRow = row;
        page.Locate = bring;
        if (!_mutatingDisplay && _displayed.Contains(row))
        {
            try { Bring(page); }
            catch (Exception ex) { Fail(page, ex); }
        }
    }

    private void Restart()
    {
        CancelPage();
        var page = new Page(_model.SignalRows.ToArray());
        _page = page;
        Error = "";
        // Swap an already visible page in one notification. Clearing it first
        // causes an empty grouped-grid layout and leaves a gap during navigation.
        if (!_mutatingDisplay && (_displayed.Count != 0 || page.Rows.Length == 0)) Publish(page);
        else Schedule(page);
        Notify();
    }

    private void CancelPage()
    {
        if (_page is not { } old) return;
        _page = null;
        old.Stop.Cancel();
        old.Ready.TrySetCanceled(old.Stop.Token);
        old.Stop.Dispose();
        old.Locate = null;
        old.LocateRow = null;
    }

    private void Schedule(Page page)
    {
        try { _post(() => Publish(page), page.Stop.Token); }
        catch (Exception ex) { Fail(page, ex); }
    }

    private void Publish(Page page)
    {
        if (!_active || !ReferenceEquals(page, _page)) return;
        try
        {
            _mutatingDisplay = true;
            try
            {
                // One reset lets the virtualizing grid create just its visible
                // containers. Rows are VM-owned references, never settings copies.
                _displayed.ReplaceWith(page.Rows);
            }
            finally
            {
                _mutatingDisplay = false;
                DrainClear();
            }
            if (!ReferenceEquals(page, _page)) return;
            if (page.LocateRow != null && _displayed.Contains(page.LocateRow)) Bring(page);
            if (!ReferenceEquals(page, _page)) return;
            page.Ready.TrySetResult();
            Notify();
        }
        catch (Exception ex) { Fail(page, ex); }
    }

    private void RequestClear()
    {
        _clearRequested = true;
        DrainClear();
    }

    private void DrainClear()
    {
        // A collection notification can synchronously trigger a host refresh or
        // unload. Never reset a WPF grouped view inside its own notification.
        if (_mutatingDisplay) return;
        _mutatingDisplay = true;
        try
        {
            while (_clearRequested)
            {
                _clearRequested = false;
                _displayed.ReplaceWith([]);
            }
        }
        finally { _mutatingDisplay = false; }
    }

    private static void Bring(Page page)
    {
        var row = page.LocateRow;
        var bring = page.Locate;
        page.LocateRow = null;
        page.Locate = null;
        if (row != null) bring?.Invoke(row);
    }

    private void Fail(Page page, Exception ex)
    {
        if (!ReferenceEquals(page, _page)) return;
        Error = ex.Message;
        page.Ready.TrySetException(ex);
        _ = page.Ready.Task.Exception;
        Notify();
    }

    private void Notify()
    {
        PropertyChanged?.Invoke(this, new(nameof(PresentedRowCount)));
        PropertyChanged?.Invoke(this, new(nameof(IsPending)));
        PropertyChanged?.Invoke(this, new(nameof(Error)));
        PropertyChanged?.Invoke(this, new(nameof(Status)));
    }
}
