using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

public class CanCyclicSendStartViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private CanCyclicSendStartSetting? _setting;

    public ObservableCollection<CyclicMessageItemViewModel> Messages { get; } = [];

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (CanCyclicSendStartSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (CanCyclicSendStartSetting)_serializer.CreateDefault();

            Messages.Clear();
            foreach (var item in _setting.Messages)
            {
                var vm = new CyclicMessageItemViewModel(item);
                vm.PropertyChanged += (_, _) => SyncAndSave();
                Messages.Add(vm);
            }

            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void SyncAndSave()
    {
        if (_setting == null) return;
        _setting.Messages = Messages.Select(vm => vm.ToModel()).ToList();
        QueueSave();
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); } catch (TaskCanceledException) { } });
    }

    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public string TaskName { get => _setting?.TaskName ?? ""; set { if (_setting == null || _setting.TaskName == value) return; _setting.TaskName = value; OnPropertyChanged(); QueueSave(); } }
    public bool EnableLog { get => _setting?.EnableLog ?? false; set { if (_setting == null || _setting.EnableLog == value) return; _setting.EnableLog = value; OnPropertyChanged(); QueueSave(); } }

    public void AddMessage()
    {
        var item = new CyclicMessageItem();
        var vm = new CyclicMessageItemViewModel(item);
        vm.PropertyChanged += (_, _) => SyncAndSave();
        Messages.Add(vm);
        SyncAndSave();
    }

    public void RemoveMessage(CyclicMessageItemViewModel vm)
    {
        Messages.Remove(vm);
        SyncAndSave();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class CyclicMessageItemViewModel : INotifyPropertyChanged
{
    private string _canId;
    private string _data;
    private int _cycleTimeMs;
    private bool _enabled;
    private CanFrameType _frameType;

    public CyclicMessageItemViewModel(CyclicMessageItem model)
    {
        _canId = model.CanId;
        _data = model.Data;
        _cycleTimeMs = model.CycleTimeMs;
        _enabled = model.Enabled;
        _frameType = model.FrameType;
    }

    public string CanId { get => _canId; set { if (_canId == value) return; _canId = value; OnPropertyChanged(); } }
    public string Data { get => _data; set { if (_data == value) return; _data = value; OnPropertyChanged(); } }
    public int CycleTimeMs { get => _cycleTimeMs; set { if (_cycleTimeMs == value) return; _cycleTimeMs = value; OnPropertyChanged(); } }
    public bool Enabled { get => _enabled; set { if (_enabled == value) return; _enabled = value; OnPropertyChanged(); } }
    public int FrameType { get => (int)_frameType; set { if ((int)_frameType == value) return; _frameType = (CanFrameType)value; OnPropertyChanged(); } }

    public CyclicMessageItem ToModel() => new()
    {
        CanId = _canId,
        Data = _data,
        CycleTimeMs = _cycleTimeMs,
        Enabled = _enabled,
        FrameType = _frameType
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
