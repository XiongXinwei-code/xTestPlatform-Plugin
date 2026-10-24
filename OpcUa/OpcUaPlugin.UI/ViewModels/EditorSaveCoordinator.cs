using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Threading;

namespace OpcUa.UI.ViewModels;

/// <summary>在创建编辑器的 Dispatcher 上保存；刷新/卸载前显式 Flush，绝不在后台读取可变设置。</summary>
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
            catch (Exception ex)
            {
                // LastError and the pending snapshot remain observable/retryable. A timer
                // callback must not bring down the WPF host on a serialization failure.
                Trace.TraceError($"OPC UA 防抖保存未成功，配置尚待保存：{ex}");
            }
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
            Trace.TraceError($"OPC UA 编辑器保存失败，保留待保存配置：{ex}");
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
