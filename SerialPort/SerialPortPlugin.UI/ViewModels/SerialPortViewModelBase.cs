using System.ComponentModel;
using System.Runtime.CompilerServices;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace SerialPort.UI.ViewModels;

public abstract class SerialPortViewModelBase : INotifyPropertyChanged
{
    protected bool _suppressSave;
    protected Step? _step;
    protected IStepSettingSerializer? _serializer;

    public void AttachSerializer(IStepSettingSerializer s)
    { FlushPendingSave();
        _serializer = s;
        if (_step != null) Load();
    }

    public void AttachStep(Step step)
    { FlushPendingSave();
        _step = step;
        Load();
    }

    protected abstract void Load();

    private readonly System.Windows.Threading.Dispatcher _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
    private readonly PluginCommon.EditorSaveScheduler _saves = new();
    public void FlushPendingSave() => _saves.Flush();
    public Exception? LastSaveError => _saves.LastError;
    protected void QueueSave()
    {
        if (!_dispatcher.CheckAccess()) { _dispatcher.Invoke(QueueSave); return; }
        if (_suppressSave || _step == null || _serializer == null) return;
        var setting = GetSetting(); if (setting == null) return;
        _saves.Schedule(_step, _serializer, setting);
    }

    protected abstract object? GetSetting();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
