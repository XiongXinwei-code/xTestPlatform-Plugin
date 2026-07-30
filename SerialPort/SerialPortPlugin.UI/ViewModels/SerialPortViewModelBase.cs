using System.ComponentModel;
using System.Runtime.CompilerServices;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace SerialPort.UI.ViewModels;

public abstract class SerialPortViewModelBase : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    protected bool _suppressSave;
    protected Step? _step;
    protected IStepSettingSerializer? _serializer;

    public void AttachSerializer(IStepSettingSerializer s)
    {
        _serializer = s;
        if (_step != null) Load();
    }

    public void AttachStep(Step step)
    {
        _step = step;
        Load();
    }

    protected abstract void Load();

    protected void QueueSave()
    {
        if (_suppressSave || _step == null || _serializer == null) return;
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        var setting = GetSetting();
        if (setting == null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SaveDebounceMs, cts.Token);
                _step.StepSetting.Setting = _serializer.Serialize(setting);
            }
            catch (TaskCanceledException) { }
        });
    }

    protected abstract object? GetSetting();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
