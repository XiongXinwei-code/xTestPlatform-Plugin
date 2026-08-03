using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.ViewModels;

public class OpcUaConnectViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private OpcUaConnectSetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    public void AttachStep(Step step) { _step = step; Load(); }

    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (OpcUaConnectSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (OpcUaConnectSetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    private void QueueSave()
    {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(_setting); } catch (TaskCanceledException) { } });
    }

    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting == null || _setting.ConnectionName == value) return; _setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }
    public string EndpointUrl { get => _setting?.EndpointUrl ?? ""; set { if (_setting == null || _setting.EndpointUrl == value) return; _setting.EndpointUrl = value; OnPropertyChanged(); QueueSave(); } }
    public int SecurityPolicy { get => (int)(_setting?.SecurityPolicy ?? OpcUaSecurityPolicy.None); set { if (_setting == null) return; _setting.SecurityPolicy = (OpcUaSecurityPolicy)value; OnPropertyChanged(); QueueSave(); } }
    public int AuthMode { get => (int)(_setting?.AuthMode ?? OpcUaAuthMode.Anonymous); set { if (_setting == null) return; _setting.AuthMode = (OpcUaAuthMode)value; OnPropertyChanged(); OnPropertyChanged(nameof(IsUserPassword)); QueueSave(); } }
    public string UserName { get => _setting?.UserName ?? ""; set { if (_setting == null || _setting.UserName == value) return; _setting.UserName = value; OnPropertyChanged(); QueueSave(); } }
    public string Password { get => _setting?.Password ?? ""; set { if (_setting == null || _setting.Password == value) return; _setting.Password = value; OnPropertyChanged(); QueueSave(); } }
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 5000; set { if (_setting == null || _setting.TimeoutMs == value) return; _setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public bool AutoAcceptCertificate { get => _setting?.AutoAcceptCertificate ?? true; set { if (_setting == null || _setting.AutoAcceptCertificate == value) return; _setting.AutoAcceptCertificate = value; OnPropertyChanged(); QueueSave(); } }

    public bool IsUserPassword => (_setting?.AuthMode ?? OpcUaAuthMode.Anonymous) == OpcUaAuthMode.UserPassword;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
