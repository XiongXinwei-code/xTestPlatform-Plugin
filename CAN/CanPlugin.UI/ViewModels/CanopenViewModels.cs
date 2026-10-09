using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
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
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    /// <summary>加载设置期间抑制回写，避免打开步骤即被标记为修改</summary>
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    protected TSetting? Setting;

    /// <summary>绑定设置序列化器</summary>
    public void AttachSerializer(IStepSettingSerializer s) { _serializer = s; if (_step != null) Load(); }
    /// <summary>绑定当前编辑的步骤并加载设置</summary>
    public void AttachStep(Step step) { _step = step; Load(); }

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
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () => { try { await Task.Delay(SaveDebounceMs, cts.Token); _step.StepSetting.Setting = _serializer.Serialize(Setting); } catch (TaskCanceledException) { } });
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
            _selectedEdsObject = value;
            OnPropertyChanged();
            if (value == null) return;
            Index = $"0x{value.Index:X4}";
            SubIndex = value.SubIndex.ToString();
            DataType = (int)value.DataType;
        }
    }

    protected override void OnLoaded() => LoadEds();

    /// <summary>解析当前 EDS 文件；失败时仅显示提示，不影响手动填写</summary>
    private void LoadEds()
    {
        EdsObjects.Clear();
        _selectedEdsObject = null;
        var path = Setting?.EdsFilePath;
        if (string.IsNullOrWhiteSpace(path)) { EdsStatus = string.Empty; return; }
        if (!File.Exists(path)) { EdsStatus = "EDS 文件不存在"; return; }
        try
        {
            foreach (var entry in EdsParser.Parse(path))
                EdsObjects.Add(entry);
            EdsStatus = $"已加载 {EdsObjects.Count} 个对象";
        }
        catch (Exception ex)
        {
            EdsStatus = $"EDS 解析失败: {ex.Message}";
        }
    }
}

/// <summary>CANopen_SdoRead 编辑器 ViewModel</summary>
public class CanopenSdoReadViewModel : CanopenSdoViewModelBase<CanopenSdoReadSetting>
{
    public string ResultVariable { get => Setting?.ResultVariable ?? ""; set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); } }
}

/// <summary>CANopen_SdoWrite 编辑器 ViewModel</summary>
public class CanopenSdoWriteViewModel : CanopenSdoViewModelBase<CanopenSdoWriteSetting>
{
    public string Value { get => Setting?.Value ?? ""; set { if (Setting == null || Setting.Value == value) return; Setting.Value = value; OnPropertyChanged(); QueueSave(); } }
}

/// <summary>CANopen_NmtControl 编辑器 ViewModel</summary>
public class CanopenNmtControlViewModel : CanopenViewModelBase<CanopenNmtControlSetting>
{
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
