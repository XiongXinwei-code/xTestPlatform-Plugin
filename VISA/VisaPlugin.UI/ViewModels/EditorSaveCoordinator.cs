using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Threading;

namespace VISA.UI.ViewModels;

/// <summary>保存留在 Dispatcher，防抖操作固定原始绑定；刷新及卸载前显式 Flush。</summary>
internal sealed class EditorSaveCoordinator
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _timer;
    private readonly List<Action> _detach = new();
    private Action? _pending;
    public Exception? LastError { get; private set; }

    public EditorSaveCoordinator()
    {
        _timer = new DispatcherTimer(DispatcherPriority.DataBind, _dispatcher)
        { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) =>
        {
            try { Flush(); }
            catch (Exception ex) { Trace.TraceError($"VISA 防抖保存未成功，保留待保存配置与 LastError：{ex}"); }
        };
    }

    public void Queue(Action save)
    {
        _dispatcher.VerifyAccess();
        _pending = save;
        _timer.Stop(); _timer.Start();
    }

    public void Flush()
    {
        _dispatcher.VerifyAccess();
        _timer.Stop();
        if (_pending is not { } save) return;
        _pending = null; // 发布 StepSetting 可能同步重入 Refresh/Flush，旧委托不能再次执行。
        try { save(); LastError = null; }
        catch (Exception ex)
        {
            _pending ??= save;
            LastError = ex;
            Trace.TraceError($"VISA 编辑器保存失败，保留待保存配置：{ex}");
            throw;
        }
    }

    public void Detach()
    {
        foreach (var detach in _detach) detach();
        _detach.Clear();
    }

    public void Observe(IEnumerable items, Action changed)
    {
        Detach();
        foreach (var item in items)
        {
            if (item is not INotifyPropertyChanged observable) continue;
            PropertyChangedEventHandler handler = (_, _) => changed();
            observable.PropertyChanged += handler;
            _detach.Add(() => observable.PropertyChanged -= handler);
        }
        if (items is INotifyCollectionChanged collection)
        {
            NotifyCollectionChangedEventHandler handler = (_, _) => { Observe(items, changed); changed(); };
            collection.CollectionChanged += handler;
            _detach.Add(() => collection.CollectionChanged -= handler);
        }
    }
}
