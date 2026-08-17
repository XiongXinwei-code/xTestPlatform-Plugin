using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Http.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI.ViewModels;

/// <summary>
/// SOAP 请求编辑器 ViewModel
/// </summary>
public class HttpSoapRequestViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private HttpSoapRequestSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (HttpSoapRequestSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (HttpSoapRequestSetting)_serializer.CreateDefault();
            HookHeaders();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void HookHeaders()
    {
        if (_setting == null) return;
        _setting.Headers.CollectionChanged -= OnHeadersChanged;
        _setting.Headers.CollectionChanged += OnHeadersChanged;
        foreach (var item in _setting.Headers)
        {
            item.PropertyChanged -= OnItemChanged;
            item.PropertyChanged += OnItemChanged;
        }
    }

    private void OnHeadersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in e.NewItems?.OfType<HttpHeaderItem>() ?? [])
        {
            item.PropertyChanged -= OnItemChanged;
            item.PropertyChanged += OnItemChanged;
        }
        QueueSave();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => QueueSave();

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); } catch (TaskCanceledException) { } });
    }

    public string ClientName { get => _setting?.ClientName ?? ""; set { if (_setting == null || _setting.ClientName == value) return; _setting.ClientName = value; OnPropertyChanged(); QueueSave(); } }
    public string Path { get => _setting?.Path ?? ""; set { if (_setting == null || _setting.Path == value) return; _setting.Path = value; OnPropertyChanged(); QueueSave(); } }
    public SoapVersion SoapVersion { get => _setting?.SoapVersion ?? SoapVersion.Soap11; set { if (_setting == null || _setting.SoapVersion == value) return; _setting.SoapVersion = value; OnPropertyChanged(); QueueSave(); } }
    public string SoapAction { get => _setting?.SoapAction ?? ""; set { if (_setting == null || _setting.SoapAction == value) return; _setting.SoapAction = value; OnPropertyChanged(); QueueSave(); } }
    public string Envelope { get => _setting?.Envelope ?? ""; set { if (_setting == null || _setting.Envelope == value) return; _setting.Envelope = value; OnPropertyChanged(); QueueSave(); } }
    public string ResponseVariable { get => _setting?.ResponseVariable ?? ""; set { if (_setting == null || _setting.ResponseVariable == value) return; _setting.ResponseVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string StatusCodeVariable { get => _setting?.StatusCodeVariable ?? ""; set { if (_setting == null || _setting.StatusCodeVariable == value) return; _setting.StatusCodeVariable = value; OnPropertyChanged(); QueueSave(); } }
    public bool TreatSoapFaultAsFailure { get => _setting?.TreatSoapFaultAsFailure ?? true; set { if (_setting == null || _setting.TreatSoapFaultAsFailure == value) return; _setting.TreatSoapFaultAsFailure = value; OnPropertyChanged(); QueueSave(); } }
    public bool TreatNonSuccessAsFailure { get => _setting?.TreatNonSuccessAsFailure ?? true; set { if (_setting == null || _setting.TreatNonSuccessAsFailure == value) return; _setting.TreatNonSuccessAsFailure = value; OnPropertyChanged(); QueueSave(); } }
    public bool LogPayload { get => _setting?.LogPayload ?? true; set { if (_setting == null || _setting.LogPayload == value) return; _setting.LogPayload = value; OnPropertyChanged(); QueueSave(); } }
    public ObservableCollection<HttpHeaderItem> Headers => _setting?.Headers ?? [];

    public IEnumerable<SoapVersion> SoapVersions => Enum.GetValues<SoapVersion>();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
