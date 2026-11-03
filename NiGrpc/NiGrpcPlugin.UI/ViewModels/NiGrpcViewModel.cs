using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using NiGrpc.Runtime;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiGrpc.UI.ViewModels;

public sealed class NiGrpcViewModel : INotifyPropertyChanged
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly PluginCommon.EditorSaveScheduler _saves = new();
    public Action<string, Action>? ExecuteCommand { get => _saves.ExecuteCommand; set => _saves.ExecuteCommand = value; }
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private NiGrpcSetting? _setting;
    private bool _loading;
    public event PropertyChangedEventHandler? PropertyChanged;
    public Exception? LastSaveError => _saves.LastError;
    public void FlushPendingSave() => _saves.Flush();
    public void Attach(Step step, IStepSettingSerializer serializer)
    {
        FlushPendingSave(); _step = step; _serializer = serializer; _loading = true;
        try
        {
            _setting = step.StepSetting.Setting is { Length: > 0 } data
                ? (NiGrpcSetting)serializer.Deserialize(data, step.StepSetting.SettingVersion)
                : (NiGrpcSetting)serializer.CreateDefault();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
        finally { _loading = false; }
    }
    private void Save([CallerMemberName] string? name = null)
    {
        if (!_dispatcher.CheckAccess()) { _dispatcher.Invoke(() => Save(name)); return; }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (!_loading && _step != null && _serializer != null && _setting != null) _saves.Schedule(_step, _serializer, _setting);
    }
    public string ConnectionName { get => _setting?.ConnectionName ?? ""; set { if (_setting != null && _setting.ConnectionName != value) { _setting.ConnectionName = value; Save(); } } }
    public string Endpoint { get => (_setting as NiGrpcConnectSetting)?.Endpoint ?? ""; set { if (_setting is NiGrpcConnectSetting s && s.Endpoint != value) { s.Endpoint = value; Save(); } } }
    public string SessionName { get => (_setting as NiGrpcSessionSetting)?.SessionName ?? ""; set { if (_setting is NiGrpcSessionSetting s && s.SessionName != value) { s.SessionName = value; Save(); } } }
    public string ConfigurationJson { get => (_setting as NiGrpcXnetOpenSetting)?.ConfigurationJson ?? ""; set { if (_setting is NiGrpcXnetOpenSetting s && s.ConfigurationJson != value) { s.ConfigurationJson = value; Save(); } } }
    public string RequestJson { get => (_setting as NiGrpcRequestSetting)?.RequestJson ?? ""; set { if (_setting is NiGrpcRequestSetting s && s.RequestJson != value) { s.RequestJson = value; Save(); } } }
    public string ResultVariable { get => (_setting as NiGrpcRequestSetting)?.ResultVariable ?? ""; set { if (_setting is NiGrpcRequestSetting s && s.ResultVariable != value) { s.ResultVariable = value; Save(); } } }
    public int TimeoutMs { get => _setting?.TimeoutMs ?? 5000; set { if (_setting != null && _setting.TimeoutMs != value) { _setting.TimeoutMs = value; Save(); } } }
}
