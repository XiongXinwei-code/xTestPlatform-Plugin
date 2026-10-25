using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using CAN.CANopen;
using CAN.CANopen.Models;
using CAN.UI.Services;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

/// <summary>CANopen 编辑器 ViewModel 基类，包含公共连接字段与防抖保存</summary>
public abstract class CanopenViewModelBase<TSetting> : INotifyPropertyChanged where TSetting : CanopenCommonSetting, new()
{
    /// <summary>保存防抖间隔，连续输入时只在停顿后序列化一次</summary>
    /// <summary>加载设置期间抑制回写，避免打开步骤即被标记为修改</summary>
    private bool _suppressSave;
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public void Flush() => _save.Flush();
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    protected TSetting? Setting;

    /// <summary>绑定设置序列化器</summary>
    public void AttachSerializer(IStepSettingSerializer s) { Flush(); _serializer = s; if (_step != null) Load(); }
    /// <summary>绑定当前编辑的步骤并加载设置</summary>
    public void AttachStep(Step step) { Flush(); _step = step; Load(); }

    /// <summary>从步骤反序列化设置（空则使用默认值）并刷新全部绑定</summary>
    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try
        {
            Setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (TSetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
                : (TSetting)_serializer.CreateDefault();
            OnLoaded();
            OnPropertyChanged(string.Empty);
        }
        finally { _suppressSave = false; }
    }

    /// <summary>设置加载完成后的扩展点</summary>
    protected virtual void OnLoaded() { }

    /// <summary>防抖保存：取消上一次待保存任务，延时后写回步骤设置</summary>
    protected void QueueSave()
    {
        if (_suppressSave || _step == null || Setting == null || _serializer == null) return;
        _save.Schedule(_step, _serializer, Setting);
    }

    /// <summary>CAN 连接名（表达式）</summary>
    public string ConnectionName { get => Setting?.ConnectionName ?? ""; set { if (Setting == null || Setting.ConnectionName == value) return; Setting.ConnectionName = value; OnPropertyChanged(); QueueSave(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>SDO 编辑器 ViewModel 基类，包含对象定位字段与 EDS 选择</summary>
public abstract class CanopenSdoViewModelBase<TSetting> : CanopenViewModelBase<TSetting> where TSetting : CanopenSdoSettingBase, new()
{
    private EdsObjectEntry? _selectedEdsObject;
    private string _edsStatus = string.Empty;
    private readonly Dispatcher _edsDispatcher = Dispatcher.CurrentDispatcher;
    private CancellationTokenSource? _activeEdsRead;
    private EdsLoadRequest? _pendingEdsRead;
    private bool _edsReading;
    private bool _edsSuspended;
    private long _edsGeneration;
    private long _edsDisplayGeneration = -1;
    private Task _edsLoadTask = Task.CompletedTask;
    private sealed record EdsLoadRequest(TSetting? Setting, string? Path, long Generation)
    {
        internal Task Initialization { get; set; } = Task.CompletedTask;
    }
    internal Task EdsLoadCompletion => _edsLoadTask;

    /// <summary>从 EDS 解析出的可选对象</summary>
    public ObservableCollection<EdsObjectEntry> EdsObjects { get; } = new();

    public string NodeId { get => Setting?.NodeId ?? ""; set { if (Setting == null || Setting.NodeId == value) return; Setting.NodeId = value; OnPropertyChanged(); QueueSave(); } }
    public string Index { get => Setting?.Index ?? ""; set { if (Setting == null || Setting.Index == value) return; Setting.Index = value; OnPropertyChanged(); QueueSave(); } }
    public string SubIndex { get => Setting?.SubIndex ?? ""; set { if (Setting == null || Setting.SubIndex == value) return; Setting.SubIndex = value; OnPropertyChanged(); QueueSave(); } }
    // DataType/TransferMode 以枚举序号绑定 ComboBox.SelectedIndex，XAML 项顺序须与枚举一致
    public int DataType { get => (int)(Setting?.DataType ?? CanopenDataType.Unsigned32); set { if (Setting == null || (int)Setting.DataType == value) return; Setting.DataType = (CanopenDataType)value; OnPropertyChanged(); QueueSave(); } }
    public int TransferMode { get => (int)(Setting?.TransferMode ?? CanopenSdoTransferMode.Auto); set { if (Setting == null || (int)Setting.TransferMode == value) return; Setting.TransferMode = (CanopenSdoTransferMode)value; OnPropertyChanged(); QueueSave(); } }
    public int ResponseTimeoutMs { get => Setting?.ResponseTimeoutMs ?? 1000; set { if (Setting == null || Setting.ResponseTimeoutMs == value) return; Setting.ResponseTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }

    /// <summary>EDS/DCF 文件路径，变更后重新解析对象列表</summary>
    public string EdsFilePath
    {
        get => Setting?.EdsFilePath ?? "";
        set
        {
            if (Setting == null || Setting.EdsFilePath == value) return;
            Setting.EdsFilePath = value;
            OnPropertyChanged();
            QueueSave();
            LoadEds();
        }
    }

    /// <summary>EDS 加载状态提示</summary>
    public string EdsStatus { get => _edsStatus; private set { _edsStatus = value; OnPropertyChanged(); } }

    /// <summary>选中 EDS 对象时自动填入 Index、SubIndex 和数据类型</summary>
    public EdsObjectEntry? SelectedEdsObject
    {
        get => _selectedEdsObject;
        set
        {
            if (value != null && (_edsDisplayGeneration != _edsGeneration || !EdsObjects.Contains(value))) return;
            var setting = Setting;
            long generation = _edsGeneration;
            bool IsCurrent() => generation == _edsGeneration && ReferenceEquals(Setting, setting) &&
                ReferenceEquals(_selectedEdsObject, value);
            _selectedEdsObject = value;
            OnPropertyChanged();
            if (value == null || !IsCurrent()) return;
            Index = $"0x{value.Index:X4}";
            if (!IsCurrent()) return;
            SubIndex = value.SubIndex.ToString();
            if (!IsCurrent()) return;
            DataType = (int)value.DataType;
        }
    }

    protected override void OnLoaded() => LoadEds();

    /// <summary>卸载仅取消结果归属；不在界面线程等待不可取消的文件打开退出。</summary>
    public void SuspendEdsLoading()
    {
        _edsDispatcher.VerifyAccess();
        _edsSuspended = true;
        ++_edsGeneration;
        _edsDisplayGeneration = -1;
        _pendingEdsRead = null;
        _activeEdsRead?.Cancel();
    }

    /// <summary>同一编辑器重新进入时重新读取当前路径，不复用已取消的旧结果。</summary>
    public void ResumeEdsLoading()
    {
        _edsDispatcher.VerifyAccess();
        if (!_edsSuspended) return;
        _edsSuspended = false;
        LoadEds();
    }

    /// <summary>只捕获不可变路径和设置身份；一次实际读取退出后才读取合并后的最新请求。</summary>
    private void LoadEds()
    {
        _edsDispatcher.VerifyAccess();
        long generation = ++_edsGeneration;
        _edsDisplayGeneration = -1;
        _pendingEdsRead = null;
        _activeEdsRead?.Cancel();
        if (generation != _edsGeneration) return;
        if (_edsSuspended)
        {
            EdsStatus = string.Empty;
            return;
        }
        var request = new EdsLoadRequest(Setting, Setting?.EdsFilePath, generation);
        _pendingEdsRead = request;
        // Queue resets instead of mutating a collection in an Add/Reset observer.
        // It runs independently of a blocked file read, but never inside its notification.
        request.Initialization = _edsDispatcher.InvokeAsync(() => InitializeEdsView(request)).Task;
        if (_edsReading) return;
        _edsReading = true;
        // Start on the Dispatcher so all continuations publish collections there.
        // Task.Run owns only file parsing and never reads the mutable Setting.
        _edsLoadTask = _edsDispatcher.InvokeAsync(ProcessEdsQueueAsync).Task.Unwrap();
        _ = _edsLoadTask.ContinueWith(t => Trace.TraceError($"EDS 编辑器后台任务失败：{t.Exception}"),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private bool IsCurrentEds(EdsLoadRequest request) => !_edsSuspended &&
        request.Generation == _edsGeneration && ReferenceEquals(Setting, request.Setting) &&
        string.Equals(Setting?.EdsFilePath, request.Path, StringComparison.Ordinal);

    private void InitializeEdsView(EdsLoadRequest request)
    {
        if (!IsCurrentEds(request)) return;
        EdsObjects.Clear();
        if (!IsCurrentEds(request)) return;
        _selectedEdsObject = null;
        OnPropertyChanged(nameof(SelectedEdsObject));
        if (!IsCurrentEds(request)) return;
        EdsStatus = string.IsNullOrWhiteSpace(request.Path) ? string.Empty : "正在后台加载 EDS（先前读取未退出时等待其收尾）…";
    }

    private async Task ProcessEdsQueueAsync()
    {
        try
        {
            while (_pendingEdsRead is { } request)
            {
                _pendingEdsRead = null;
                await request.Initialization;
                if (!IsCurrentEds(request) || request.Setting == null || string.IsNullOrWhiteSpace(request.Path)) continue;
                using var cancellation = new CancellationTokenSource();
                _activeEdsRead = cancellation;
                IReadOnlyList<EdsObjectEntry>? entries = null;
                string? failure = null;
                try
                {
                    entries = await Task.Run(() =>
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        var result = ReadEdsObjects(request.Path, cancellation.Token);
                        cancellation.Token.ThrowIfCancellationRequested();
                        return result;
                    });
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (FileNotFoundException) { failure = "EDS 文件不存在"; }
                catch (DirectoryNotFoundException) { failure = "EDS 文件不存在"; }
                catch (Exception ex) { failure = $"EDS 解析失败: {ex.Message}"; }
                finally { _activeEdsRead = null; }

                _edsDispatcher.VerifyAccess();
                bool IsCurrent() => !cancellation.IsCancellationRequested && IsCurrentEds(request);
                if (!IsCurrent()) continue;
                if (entries != null)
                {
                    _edsDisplayGeneration = request.Generation;
                    foreach (var entry in entries)
                    {
                        if (!IsCurrent()) break;
                        EdsObjects.Add(entry); // A collection notification may re-enter Refresh/Unloaded.
                    }
                    if (IsCurrent()) EdsStatus = $"已加载 {EdsObjects.Count} 个对象";
                }
                else EdsStatus = failure ?? "EDS 读取已取消";
            }
        }
        finally { _edsReading = false; }
    }

    /// <summary>读取对象字典的独立边界；编辑器生命周期负责调度与结果归属。</summary>
    protected virtual IReadOnlyList<EdsObjectEntry> ReadEdsObjects(string path, CancellationToken cancellationToken)
        => EdsParser.Parse(path, cancellationToken);
}

/// <summary>CANopen_SdoRead 编辑器 ViewModel</summary>
public class CanopenSdoReadViewModel : CanopenSdoViewModelBase<CanopenSdoReadSetting>
{
    public string ResultVariable { get => Setting?.ResultVariable ?? ""; set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
}

/// <summary>CANopen_SyncSend 编辑器 ViewModel</summary>
public class CanopenSyncSendViewModel : CanopenViewModelBase<CanopenSyncSendSetting>
{
    public int SendTimeoutMs { get => Setting?.SendTimeoutMs ?? 1000; set { if (Setting == null || Setting.SendTimeoutMs == value) return; Setting.SendTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public string CobId { get => Setting?.CobId ?? ""; set { if (Setting == null || Setting.CobId == value) return; Setting.CobId = value; OnPropertyChanged(); QueueSave(); } }
    public string Counter { get => Setting?.Counter ?? ""; set { if (Setting == null || Setting.Counter == value) return; Setting.Counter = value; OnPropertyChanged(); QueueSave(); } }
}

/// <summary>CANopen_EmcyReceive 编辑器 ViewModel</summary>
public class CanopenEmcyReceiveViewModel : CanopenViewModelBase<CanopenEmcyReceiveSetting>
{
    public string NodeId { get => Setting?.NodeId ?? ""; set { if (Setting == null || Setting.NodeId == value) return; Setting.NodeId = value; OnPropertyChanged(); QueueSave(); } }
    public string ExpectedErrorCode { get => Setting?.ExpectedErrorCode ?? ""; set { if (Setting == null || Setting.ExpectedErrorCode == value) return; Setting.ExpectedErrorCode = value; OnPropertyChanged(); QueueSave(); } }
    public int TimeoutMs { get => Setting?.TimeoutMs ?? 3000; set { if (Setting == null || Setting.TimeoutMs == value) return; Setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public string ErrorCodeVariable { get => Setting?.ErrorCodeVariable ?? ""; set { if (Setting == null || Setting.ErrorCodeVariable == value) return; Setting.ErrorCodeVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string ErrorRegisterVariable { get => Setting?.ErrorRegisterVariable ?? ""; set { if (Setting == null || Setting.ErrorRegisterVariable == value) return; Setting.ErrorRegisterVariable = value; OnPropertyChanged(); QueueSave(); } }
    public string ManufacturerDataVariable { get => Setting?.ManufacturerDataVariable ?? ""; set { if (Setting == null || Setting.ManufacturerDataVariable == value) return; Setting.ManufacturerDataVariable = value; OnPropertyChanged(); QueueSave(); } }
}

/// <summary>CANopen_SdoWrite 编辑器 ViewModel</summary>
public class CanopenSdoWriteViewModel : CanopenSdoViewModelBase<CanopenSdoWriteSetting>
{
    public string Value { get => Setting?.Value ?? ""; set { if (Setting == null || Setting.Value == value) return; Setting.Value = value; OnPropertyChanged(); QueueSave(); } }
}

/// <summary>CANopen_NmtControl 编辑器 ViewModel</summary>
public class CanopenNmtControlViewModel : CanopenViewModelBase<CanopenNmtControlSetting>
{
    public int SendTimeoutMs { get => Setting?.SendTimeoutMs ?? 1000; set { if (Setting == null || Setting.SendTimeoutMs == value) return; Setting.SendTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public string NodeId { get => Setting?.NodeId ?? ""; set { if (Setting == null || Setting.NodeId == value) return; Setting.NodeId = value; OnPropertyChanged(); QueueSave(); } }
    public int Command { get => (int)(Setting?.Command ?? CanopenNmtCommand.Start); set { if (Setting == null || (int)Setting.Command == value) return; Setting.Command = (CanopenNmtCommand)value; OnPropertyChanged(); QueueSave(); } }
}

/// <summary>CANopen_WaitHeartbeat 编辑器 ViewModel</summary>
public class CanopenWaitHeartbeatViewModel : CanopenViewModelBase<CanopenWaitHeartbeatSetting>
{
    public string NodeId { get => Setting?.NodeId ?? ""; set { if (Setting == null || Setting.NodeId == value) return; Setting.NodeId = value; OnPropertyChanged(); QueueSave(); } }
    public int ExpectedState { get => (int)(Setting?.ExpectedState ?? CanopenNmtState.Operational); set { if (Setting == null || (int)Setting.ExpectedState == value) return; Setting.ExpectedState = (CanopenNmtState)value; OnPropertyChanged(); QueueSave(); } }
    public int TimeoutMs { get => Setting?.TimeoutMs ?? 3000; set { if (Setting == null || Setting.TimeoutMs == value) return; Setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public string StateVariable { get => Setting?.StateVariable ?? ""; set { if (Setting == null || Setting.StateVariable == value) return; Setting.StateVariable = value; OnPropertyChanged(); QueueSave(); } }
}

/// <summary>CANopen_PdoSend 编辑器 ViewModel</summary>
public class CanopenPdoSendViewModel : CanopenViewModelBase<CanopenPdoSendSetting>
{
    public int SendTimeoutMs { get => Setting?.SendTimeoutMs ?? 1000; set { if (Setting == null || Setting.SendTimeoutMs == value) return; Setting.SendTimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public string CobId { get => Setting?.CobId ?? ""; set { if (Setting == null || Setting.CobId == value) return; Setting.CobId = value; OnPropertyChanged(); QueueSave(); } }
    public string Data { get => Setting?.Data ?? ""; set { if (Setting == null || Setting.Data == value) return; Setting.Data = value; OnPropertyChanged(); QueueSave(); } }
}

/// <summary>CANopen_PdoReceive 编辑器 ViewModel</summary>
public class CanopenPdoReceiveViewModel : CanopenViewModelBase<CanopenPdoReceiveSetting>
{
    public string CobId { get => Setting?.CobId ?? ""; set { if (Setting == null || Setting.CobId == value) return; Setting.CobId = value; OnPropertyChanged(); QueueSave(); } }
    public int TimeoutMs { get => Setting?.TimeoutMs ?? 1000; set { if (Setting == null || Setting.TimeoutMs == value) return; Setting.TimeoutMs = value; OnPropertyChanged(); QueueSave(); } }
    public string ResultVariable { get => Setting?.ResultVariable ?? ""; set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
}
