using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Http.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI.ViewModels;

/// <summary>
/// HTTP REST 请求编辑器 ViewModel
/// </summary>
public class HttpRequestViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private HttpRequestSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (HttpRequestSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (HttpRequestSetting)_serializer.CreateDefault();
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
    public HttpMethodType Method { get => _setting?.Method ?? HttpMethodType.Get; set { if (_setting == null || _setting.Method == value) return; _setting.Method = value; OnPropertyChanged(); QueueSave(); } }
    public string Path { get => _setting?.Path ?? ""; set { if (_setting == null || _setting.Path == value) return; _setting.Path = value; OnPropertyChanged(); QueueSave(); } }
    public BodyContentType ContentType { get => _setting?.ContentType ?? BodyContentType.None; set { if (_setting == null || _setting.ContentType == value) return; _setting.ContentType = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasBody)); QueueSave(); } }
    public string Body { get => _setting?.Body ?? ""; set { if (_setting == null || _setting.Body == value) return; _setting.Body = value; OnPropertyChanged(); QueueSave(); } }
    public string ResponseVariable { get => _setting?.ResponseVariable ?? ""; set { if (_setting == null || _setting.ResponseVariable == value) return; _setting.ResponseVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string StatusCodeVariable { get => _setting?.StatusCodeVariable ?? ""; set { if (_setting == null || _setting.StatusCodeVariable == value) return; _setting.StatusCodeVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string ElapsedVariable { get => _setting?.ElapsedVariable ?? ""; set { if (_setting == null || _setting.ElapsedVariable == value) return; _setting.ElapsedVariable = value; OnPropertyChanged(); QueueSave(); } }
    public bool TreatNonSuccessAsFailure { get => _setting?.TreatNonSuccessAsFailure ?? true; set { if (_setting == null || _setting.TreatNonSuccessAsFailure == value) return; _setting.TreatNonSuccessAsFailure = value; OnPropertyChanged(); QueueSave(); } }
    public bool LogPayload { get => _setting?.LogPayload ?? true; set { if (_setting == null || _setting.LogPayload == value) return; _setting.LogPayload = value; OnPropertyChanged(); QueueSave(); } }
    public ObservableCollection<HttpHeaderItem> Headers => _setting?.Headers ?? [];

    public IEnumerable<HttpMethodType> Methods => Enum.GetValues<HttpMethodType>();
    public IEnumerable<BodyContentType> ContentTypes => Enum.GetValues<BodyContentType>();

    /// <summary>请求体类型为 None 时隐藏请求体输入区</summary>
    public bool HasBody => ContentType != BodyContentType.None;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
