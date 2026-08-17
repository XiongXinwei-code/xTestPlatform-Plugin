using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Http.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI.ViewModels;

/// <summary>
/// HTTP 客户端创建编辑器 ViewModel
/// </summary>
public class HttpClientCreateViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private HttpClientCreateSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (HttpClientCreateSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (HttpClientCreateSetting)_serializer.CreateDefault();
            HookHeaders();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void HookHeaders()
    {
        if (_setting == null) return;
        _setting.DefaultHeaders.CollectionChanged -= OnHeadersChanged;
        _setting.DefaultHeaders.CollectionChanged += OnHeadersChanged;
        foreach (var item in _setting.DefaultHeaders)
        {
            item.PropertyChanged -= OnHeaderItemChanged;
            item.PropertyChanged += OnHeaderItemChanged;
        }
    }

    private void OnHeadersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in e.NewItems?.OfType<HttpHeaderItem>() ?? [])
        {
            item.PropertyChanged -= OnHeaderItemChanged;
            item.PropertyChanged += OnHeaderItemChanged;
        }
        QueueSave();
    }

    private void OnHeaderItemChanged(object? sender, PropertyChangedEventArgs e) => QueueSave();

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); } catch (TaskCanceledException) { } });
    }

    public string ClientName { get => _setting?.ClientName ?? ""; set { if (_setting == null || _setting.ClientName == value) return; _setting.ClientName = value; OnPropertyChanged(); QueueSave(); } }
    public string BaseUrl { get => _setting?.BaseUrl ?? ""; set { if (_setting == null || _setting.BaseUrl == value) return; _setting.BaseUrl = value; OnPropertyChanged(); QueueSave(); } }
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 30000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public AuthMode AuthMode { get => _setting?.AuthMode ?? AuthMode.None; set { if (_setting == null || _setting.AuthMode == value) return; _setting.AuthMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsBasic)); OnPropertyChanged(nameof(IsToken)); OnPropertyChanged(nameof(IsCertificate)); QueueSave(); } }
    public string UserName { get => _setting?.UserName ?? ""; set { if (_setting == null || _setting.UserName == value) return; _setting.UserName = value; OnPropertyChanged(); QueueSave(); } }
    public string Password { get => _setting?.Password ?? ""; set { if (_setting == null || _setting.Password == value) return; _setting.Password = value; OnPropertyChanged(); QueueSave(); } }
    public string Token { get => _setting?.Token ?? ""; set { if (_setting == null || _setting.Token == value) return; _setting.Token = value; OnPropertyChanged(); QueueSave(); } }
    public string ClientCertPath { get => _setting?.ClientCertPath ?? ""; set { if (_setting == null || _setting.ClientCertPath == value) return; _setting.ClientCertPath = value; OnPropertyChanged(); QueueSave(); } }
    public string ClientCertPassword { get => _setting?.ClientCertPassword ?? ""; set { if (_setting == null || _setting.ClientCertPassword == value) return; _setting.ClientCertPassword = value; OnPropertyChanged(); QueueSave(); } }
    public bool IgnoreServerCertificateErrors { get => _setting?.IgnoreServerCertificateErrors ?? false; set { if (_setting == null || _setting.IgnoreServerCertificateErrors == value) return; _setting.IgnoreServerCertificateErrors = value; OnPropertyChanged(); QueueSave(); } }
    public bool ReplaceIfExists { get => _setting?.ReplaceIfExists ?? true; set { if (_setting == null || _setting.ReplaceIfExists == value) return; _setting.ReplaceIfExists = value; OnPropertyChanged(); QueueSave(); } }
    public ObservableCollection<HttpHeaderItem> DefaultHeaders => _setting?.DefaultHeaders ?? [];

    public IEnumerable<AuthMode> AuthModes => Enum.GetValues<AuthMode>();

    /// <summary>以下三个属性供界面按认证方式切换输入区可见性</summary>
    public bool IsBasic => AuthMode == AuthMode.Basic;
    public bool IsToken => AuthMode == AuthMode.BearerToken;
    public bool IsCertificate => AuthMode == AuthMode.ClientCertificate;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
