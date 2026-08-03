using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.Protocol;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.StepPlugin.UI.ViewModels;

public sealed class UdpEditorViewModel : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private readonly IStepSettingSerializer _serializer;
    private readonly Func<byte[], string> _generateDescription;
    private readonly bool _receive;
    private readonly object _saveGate = new();
    private CancellationTokenSource? _saveCts;
    private Step? _step;
    private UdpSendSetting? _setting;
    private bool _suppressSave;

    public UdpEditorViewModel(IStepSettingSerializer serializer, Func<byte[], string> generateDescription, bool receive)
    {
        _serializer = serializer;
        ArgumentNullException.ThrowIfNull(generateDescription);
        _generateDescription = generateDescription;
        _receive = receive;
    }

    public Array PacketFormats => Enum.GetValues<UdpPacketFormat>();
    public Array ReplyMatchModes => Enum.GetValues<UdpReplyMatchMode>();
    public Visibility ReceiveVisibility => _receive ? Visibility.Visible : Visibility.Collapsed;
    public string? LoadError { get; private set; }
    public Action<string, Action>? ExecuteCommand { get; set; }

    public void AttachStep(Step step)
    {
        ArgumentNullException.ThrowIfNull(step);
        CommitPendingChanges();
        CancelPendingSave();
        _suppressSave = true;
        try
        {
            _step = step;
            _setting = step.StepSetting.Setting is { Length: > 0 } data
                ? (UdpSendSetting)_serializer.Deserialize(data, step.StepSetting.SettingVersion)
                : (UdpSendSetting)_serializer.CreateDefault();
            LoadError = null;
        }
        catch (Exception ex)
        {
            _setting = (UdpSendSetting)_serializer.CreateDefault();
            LoadError = $"无法读取 UDP 步骤配置：{ex.Message}";
            Trace.TraceError(LoadError);
        }
        finally
        {
            _suppressSave = false;
        }

        OnPropertyChanged(string.Empty);
    }

    public string RemoteAddress
    {
        get => Setting.RemoteAddress;
        set => SetValue(value ?? string.Empty, static (setting, updated) => setting.RemoteAddress = updated);
    }

    public int RemotePort
    {
        get => Setting.RemotePort;
        set => SetValue(value, static (setting, updated) => setting.RemotePort = updated);
    }

    public string LocalAddress
    {
        get => Setting.LocalAddress;
        set => SetValue(value ?? string.Empty, static (setting, updated) => setting.LocalAddress = updated);
    }

    public int LocalPort
    {
        get => Setting.LocalPort;
        set => SetValue(value, static (setting, updated) => setting.LocalPort = updated);
    }

    public string RequestData
    {
        get => Setting.RequestData;
        set => SetValue(value ?? string.Empty, static (setting, updated) => setting.RequestData = updated);
    }

    public UdpPacketFormat RequestFormat
    {
        get => Setting.RequestFormat;
        set => SetValue(value, static (setting, updated) => setting.RequestFormat = updated);
    }

    public int ReceiveTimeoutMs
    {
        get => ReceiveSetting?.ReceiveTimeoutMs ?? 0;
        set => SetReceiveValue(value, static (setting, updated) => setting.ReceiveTimeoutMs = updated);
    }

    public UdpPacketFormat ReplyFormat
    {
        get => ReceiveSetting?.ReplyFormat ?? UdpPacketFormat.Utf8Text;
        set => SetReceiveValue(value, static (setting, updated) => setting.ReplyFormat = updated);
    }

    public string ExpectedReply
    {
        get => ReceiveSetting?.ExpectedReply ?? string.Empty;
        set => SetReceiveValue(value ?? string.Empty, static (setting, updated) => setting.ExpectedReply = updated);
    }

    public UdpReplyMatchMode MatchMode
    {
        get => ReceiveSetting?.MatchMode ?? UdpReplyMatchMode.Exact;
        set => SetReceiveValue(value, static (setting, updated) => setting.MatchMode = updated);
    }

    public string ResponseVariable
    {
        get => ReceiveSetting?.ResponseVariable ?? string.Empty;
        set => SetReceiveValue(value ?? string.Empty, static (setting, updated) => setting.ResponseVariable = updated);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void CommitPendingChanges()
    {
        if (_suppressSave || _step is null || _setting is null)
        {
            return;
        }

        var step = _step;
        byte[] data;
        try
        {
            lock (_saveGate)
            {
                var pendingSave = _saveCts;
                _saveCts = null;
                pendingSave?.Cancel();
                data = _serializer.Serialize(_setting);
            }

            void ApplySetting() => step.StepSetting.Setting = data;

            var description = "更新 UDP 步骤配置";
            try
            {
                description = $"{description}: {_generateDescription(data)}";
            }
            catch (Exception ex)
            {
                Trace.TraceError($"生成 UDP 步骤配置描述失败：{ex.Message}");
            }

            var executeCommand = ExecuteCommand;
            if (executeCommand is null)
            {
                ApplySetting();
                return;
            }

            try
            {
                executeCommand(description, ApplySetting);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"通过宿主命令提交 UDP 步骤配置失败：{ex.Message}");
                ApplySetting();
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"通过宿主命令提交 UDP 步骤配置失败：{ex.Message}");
        }
    }

    private UdpSendSetting Setting => _setting ?? throw new InvalidOperationException("编辑器未绑定步骤");
    private UdpSendAndReceiveSetting? ReceiveSetting => _setting as UdpSendAndReceiveSetting;

    private void SetValue<T>(T value, Action<UdpSendSetting, T> assign, [CallerMemberName] string? propertyName = null)
    {
        assign(Setting, value);
        OnPropertyChanged(propertyName);
        QueueSave();
    }

    private void SetReceiveValue<T>(T value, Action<UdpSendAndReceiveSetting, T> assign, [CallerMemberName] string? propertyName = null)
    {
        var setting = ReceiveSetting;
        if (setting is null)
        {
            return;
        }

        assign(setting, value);
        OnPropertyChanged(propertyName);
        QueueSave();
    }

    private void QueueSave()
    {
        if (_suppressSave || _step is null || _setting is null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        lock (_saveGate)
        {
            _saveCts?.Cancel();
            _saveCts = cts;
        }

        _ = SaveAfterDelayAsync(_step, _setting, cts);
    }

    private async Task SaveAfterDelayAsync(Step step, UdpSendSetting setting, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(SaveDebounceMs, cts.Token);
            lock (_saveGate)
            {
                if (cts.IsCancellationRequested || !ReferenceEquals(_saveCts, cts))
                {
                    return;
                }

                _saveCts = null;
            }

            CommitPendingChanges();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Trace.TraceError($"保存 UDP 步骤配置失败：{ex.Message}");
        }
        finally
        {
            cts.Dispose();
        }
    }

    private void CancelPendingSave()
    {
        lock (_saveGate)
        {
            _saveCts?.Cancel();
            _saveCts = null;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
