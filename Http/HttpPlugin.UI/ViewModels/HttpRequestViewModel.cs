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
    private readonly PluginCommon.EditorSaveScheduler _saver = new();
    public Action<string, Action>? ExecuteCommand { get => _saver.ExecuteCommand; set => _saver.ExecuteCommand = value; }
    private readonly HashSet<HttpHeaderItem> _observedItems = new();
    public void FlushPendingSave() => _saver.Flush();
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private HttpRequestSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _saver.Flush(); _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _saver.Flush(); _step = step; Load(); }

    private void Load()
    {
        _saver.Flush();
        if (_serializer == null || _step == null) return;
        if (_setting != null)
        {
            _setting.Headers.CollectionChanged -= OnHeadersChanged;
            foreach (var item in _observedItems) item.PropertyChanged -= OnItemChanged;
            _observedItems.Clear();
        }
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
        foreach (var item in _observedItems) item.PropertyChanged -= OnItemChanged;
        _observedItems.Clear();
        foreach (var item in _setting.Headers)
        {
            if (_observedItems.Add(item)) item.PropertyChanged += OnItemChanged;
        }
    }

    private void OnHeadersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        HookHeaders(); // Reset/Clear has no OldItems; reconcile actual current membership.
        QueueSave();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => QueueSave();

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _saver.Schedule(_step, _serializer, _setting);
    }

    public string ClientName { get => _setting?.ClientName ?? ""; set { if (_setting == null || _setting.ClientName == value) return; _setting.ClientName = value; OnPropertyChanged(); QueueSave(); } }
    public HttpMethodType Method { get => _setting?.Method ?? HttpMethodType.Get; set { if (_setting == null || _setting.Method == value) return; _setting.Method = value; OnPropertyChanged(); QueueSave(); } }
    public string Path { get => _setting?.Path ?? ""; set { if (_setting == null || _setting.Path == value) return; _setting.Path = value; OnPropertyChanged(); QueueSave(); } }
    public BodyContentType ContentType { get => _setting?.ContentType ?? BodyContentType.None; set { if (_setting == null || _setting.ContentType == value) return; _setting.ContentType = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasBody)); OnPropertyChanged(nameof(BodyHint)); QueueSave(); } }
    public string Body { get => _setting?.Body ?? ""; set { if (_setting == null || _setting.Body == value) return; _setting.Body = value; OnPropertyChanged(); QueueSave(); } }
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 0; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public int MaxResponseBytes { get => _setting?.MaxResponseBytes ?? 16777216; set { if (_setting == null || _setting.MaxResponseBytes == value) return; _setting.MaxResponseBytes = value; OnPropertyChanged(); QueueSave(); } }
    public HttpResponseFormat ResponseFormat { get => _setting?.ResponseFormat ?? HttpResponseFormat.Text; set { if (_setting == null || _setting.ResponseFormat == value) return; _setting.ResponseFormat = value; OnPropertyChanged(); OnPropertyChanged(nameof(ResponseHint)); QueueSave(); } }
    public string ResponseHeadersVariable { get => _setting?.ResponseHeadersVariable ?? ""; set { if (_setting == null || _setting.ResponseHeadersVariable == value) return; _setting.ResponseHeadersVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string ResponseVariable { get => _setting?.ResponseVariable ?? ""; set { if (_setting == null || _setting.ResponseVariable == value) return; _setting.ResponseVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string StatusCodeVariable { get => _setting?.StatusCodeVariable ?? ""; set { if (_setting == null || _setting.StatusCodeVariable == value) return; _setting.StatusCodeVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string ElapsedVariable { get => _setting?.ElapsedVariable ?? ""; set { if (_setting == null || _setting.ElapsedVariable == value) return; _setting.ElapsedVariable = value; OnPropertyChanged(); QueueSave(); } }
    public bool TreatNonSuccessAsFailure { get => _setting?.TreatNonSuccessAsFailure ?? true; set { if (_setting == null || _setting.TreatNonSuccessAsFailure == value) return; _setting.TreatNonSuccessAsFailure = value; OnPropertyChanged(); QueueSave(); } }
    public bool LogPayload { get => _setting?.LogPayload ?? true; set { if (_setting == null || _setting.LogPayload == value) return; _setting.LogPayload = value; OnPropertyChanged(); QueueSave(); } }
    public ObservableCollection<HttpHeaderItem> Headers => _setting?.Headers ?? [];

    public IEnumerable<HttpMethodType> Methods => Enum.GetValues<HttpMethodType>();
    public IEnumerable<BodyContentType> ContentTypes => Enum.GetValues<BodyContentType>();
    public IEnumerable<HttpResponseFormat> ResponseFormats => Enum.GetValues<HttpResponseFormat>();
    public string BodyHint => ContentType == BodyContentType.Bytes
        ? "表达式返回十六进制字符串，如 \"00 01 FE FF\"；按原始字节发送，不编码为文本。"
        : ContentType == BodyContentType.FormUrlEncoded
            ? "表达式返回已经 URL 编码的表单字符串，如 \"name=Alice&value=1\"。"
            : "表达式返回正文字符串；按所选内容类型以 UTF-8 发送。";
    public string ResponseHint => ResponseFormat switch
    {
        HttpResponseFormat.Hex => "输出大写连续十六进制字符串，无空格；例如 00FEFF。",
        HttpResponseFormat.Base64 => "输出 Base64 字符串；保留原始字节，可由后续步骤解码。",
        _ => "按响应 charset / BOM 解码，未指定时使用 UTF-8；响应变量类型为 string。"
    };

    /// <summary>请求体类型为 None 时隐藏请求体输入区</summary>
    public bool HasBody => ContentType != BodyContentType.None;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
