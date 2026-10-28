using System.Collections.ObjectModel;
using System.Windows.Threading;
using CAN.Flash.Models;
using CAN.UI.Models;
using CAN.UI.Services;

namespace CAN.UI.ViewModels;

/// <summary>UDS 固件烧录编辑器 ViewModel</summary>
public sealed class CanFlashViewModel : UdsViewModelBase<CanFlashSetting>
{
    private readonly Func<string, FirmwareFormat, string, CancellationToken, Task<FirmwareAnalysisResult>> _analyze;
    private readonly string? _presetPath;

    public CanFlashViewModel() : this(FirmwareAnalyzer.AnalyzeAsync) { }

    internal CanFlashViewModel(
        Func<string, FirmwareFormat, string, CancellationToken, Task<FirmwareAnalysisResult>> analyze,
        string? presetPath = null)
    {
        _analyze = analyze;
        _presetPath = presetPath;
        ReloadPresets();
    }

    // ── 固件文件 ────────────────────────────────────────────────────
    public string FilePath
    {
        get => Setting?.FilePath ?? "";
        set { if (Setting == null || Setting.FilePath == value) return; Setting.FilePath = value; InvalidateAnalysis(); OnPropertyChanged(); QueueSave(); }
    }

    public int FormatIndex
    {
        get => (int)(Setting?.Format ?? FirmwareFormat.Auto);
        set { if (Setting == null || FormatIndex == value) return; Setting.Format = (FirmwareFormat)value; InvalidateAnalysis(); OnPropertyChanged(); OnPropertyChanged(nameof(IsBinaryFormat)); QueueSave(); }
    }

    /// <summary>基地址仅在 Binary 格式下有意义</summary>
    public bool IsBinaryFormat => Setting?.Format == FirmwareFormat.Binary;

    public string BaseAddress
    {
        get => Setting?.BaseAddress ?? "";
        set { if (Setting == null || Setting.BaseAddress == value) return; Setting.BaseAddress = value; InvalidateAnalysis(); OnPropertyChanged(); QueueSave(); }
    }

    // ── 映射与填充 ─────────────────────────────────────────────────
    public bool UseMappedRange
    {
        get => Setting?.UseMappedRange ?? false;
        set
        {
            if (Setting == null || Setting.UseMappedRange == value) return;
            Setting.UseMappedRange = value;
            // ZLG 的映射范围配置对应带参数擦除；用户随后仍可在擦除页明确改为无参数模式。
            if (value)
            {
                Setting.EraseWithAddressAndLength = true;
                OnPropertyChanged(nameof(EraseWithAddressAndLength));
            }
            OnPropertyChanged();
            QueueSave();
        }
    }

    public string MappedStartAddress
    {
        get => Setting?.MappedStartAddress ?? "";
        set { if (Setting == null || Setting.MappedStartAddress == value) return; Setting.MappedStartAddress = value; OnPropertyChanged(); QueueSave(); }
    }

    public string MappedEndAddress
    {
        get => Setting?.MappedEndAddress ?? "";
        set { if (Setting == null || Setting.MappedEndAddress == value) return; Setting.MappedEndAddress = value; OnPropertyChanged(); QueueSave(); }
    }

    public string GapFillByte
    {
        get => Setting?.GapFillByte ?? "0";
        set { if (Setting == null || Setting.GapFillByte == value) return; Setting.GapFillByte = value; OnPropertyChanged(); QueueSave(); }
    }

    // ── 下载参数 ────────────────────────────────────────────────────
    public string AddressAndLengthFormatId
    {
        get => Setting?.AddressAndLengthFormatId ?? "";
        set { if (Setting == null || Setting.AddressAndLengthFormatId == value) return; Setting.AddressAndLengthFormatId = value; OnPropertyChanged(); QueueSave(); }
    }

    public string DataFormatId
    {
        get => Setting?.DataFormatId ?? "";
        set { if (Setting == null || Setting.DataFormatId == value) return; Setting.DataFormatId = value; OnPropertyChanged(); QueueSave(); }
    }

    public int MaxBlockSize
    {
        get => Setting?.MaxBlockSize ?? 512;
        set { if (Setting == null || Setting.MaxBlockSize == value) return; Setting.MaxBlockSize = value; OnPropertyChanged(); QueueSave(); }
    }

    public int PreDownloadDelayMs
    {
        get => Setting?.PreDownloadDelayMs ?? 0;
        set { if (Setting == null || Setting.PreDownloadDelayMs == value) return; Setting.PreDownloadDelayMs = value; OnPropertyChanged(); QueueSave(); }
    }

    public int BlockRetryCount
    {
        get => Setting?.BlockRetryCount ?? 2;
        set { if (Setting == null || Setting.BlockRetryCount == value) return; Setting.BlockRetryCount = value; OnPropertyChanged(); QueueSave(); }
    }

    public int InterBlockDelayMs
    {
        get => Setting?.InterBlockDelayMs ?? 0;
        set { if (Setting == null || Setting.InterBlockDelayMs == value) return; Setting.InterBlockDelayMs = value; OnPropertyChanged(); QueueSave(); }
    }

    // ── 擦除 ────────────────────────────────────────────────────────
    public bool EraseBeforeDownload
    {
        get => Setting?.EraseBeforeDownload ?? true;
        set { if (Setting == null || Setting.EraseBeforeDownload == value) return; Setting.EraseBeforeDownload = value; OnPropertyChanged(); QueueSave(); }
    }

    public string EraseRoutineId
    {
        get => Setting?.EraseRoutineId ?? "";
        set { if (Setting == null || Setting.EraseRoutineId == value) return; Setting.EraseRoutineId = value; OnPropertyChanged(); QueueSave(); }
    }

    public bool EraseWithAddressAndLength
    {
        get => Setting?.UseMappedRange == true || Setting?.EraseWithAddressAndLength != false;
        set
        {
            if (Setting == null) return;
            if (Setting.UseMappedRange && !value)
            {
                OnPropertyChanged();
                return;
            }
            if (Setting.EraseWithAddressAndLength == value) return;
            Setting.EraseWithAddressAndLength = value;
            OnPropertyChanged();
            QueueSave();
        }
    }

    public int EraseTimeoutMs
    {
        get => Setting?.EraseTimeoutMs ?? 30000;
        set { if (Setting == null || Setting.EraseTimeoutMs == value) return; Setting.EraseTimeoutMs = value; OnPropertyChanged(); QueueSave(); }
    }

    // ── 校验 ────────────────────────────────────────────────────────
    public int CheckModeIndex
    {
        get => (int)(Setting?.CheckMode ?? FlashCheckMode.Crc32);
        set { if (Setting == null) return; Setting.CheckMode = (FlashCheckMode)value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCheckEnabled)); QueueSave(); }
    }

    public bool IsCheckEnabled => Setting?.CheckMode != FlashCheckMode.None;

    public string CheckRoutineId
    {
        get => Setting?.CheckRoutineId ?? "";
        set { if (Setting == null || Setting.CheckRoutineId == value) return; Setting.CheckRoutineId = value; OnPropertyChanged(); QueueSave(); }
    }

    // ── 输出 ────────────────────────────────────────────────────────
    public string ProgressVariable
    {
        get => Setting?.ProgressVariable ?? "";
        set { if (Setting == null || Setting.ProgressVariable == value) return; Setting.ProgressVariable = value; OnPropertyChanged(); QueueSave(); }
    }

    public string ResultVariable
    {
        get => Setting?.ResultVariable ?? "";
        set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); }
    }

    // ── 固件分析 ────────────────────────────────────────────────────
    private bool _isAnalyzing;
    private string _analysisMessage = "尚未分析。选择固件文件后点击「分析固件」可查看数据段并获取格式标识建议值。";
    private string _suggestedAlfid = string.Empty;
    private long _analysisGeneration;
    private readonly Dispatcher _analysisDispatcher = Dispatcher.CurrentDispatcher;
    private bool _analysisSuspended;
    private AnalysisOperation? _activeAnalysis;
    private Task _analysisCompletion = Task.CompletedTask;
    private int _segmentNotificationDepth;

    internal Task AnalysisCompletion => _analysisCompletion;

    private sealed class AnalysisOperation(CanFlashSetting setting, long generation)
    {
        internal readonly CanFlashSetting Setting = setting;
        internal readonly long Generation = generation;
        internal readonly CancellationTokenSource Cancellation = new();
        internal readonly TaskCompletionSource WaitCancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken Token;
        internal Task<FirmwareAnalysisResult> Worker = Task.FromResult(new FirmwareAnalysisResult());
        internal Task CancellationCompletion = Task.CompletedTask;
        internal bool CancelRequested;
    }

    protected override void OnSettingLoaded() => InvalidateAnalysis();

    private void InvalidateAnalysis()
    {
        long generation = ++_analysisGeneration;
        CancelAnalysisWait();
        if (generation != _analysisGeneration) return;
        if (_segmentNotificationDepth != 0)
        {
            _analysisDispatcher.BeginInvoke(() => ResetAnalysisView(generation));
            return;
        }
        ResetAnalysisView(generation);
    }

    private void ResetAnalysisView(long generation)
    {
        if (generation != _analysisGeneration) return;
        SuggestedAlfid = string.Empty;
        if (generation != _analysisGeneration) return;
        _segmentNotificationDepth++;
        try { AnalysisSegments.Clear(); }
        finally { _segmentNotificationDepth--; }
        if (generation != _analysisGeneration) return;
        AnalysisMessage = "固件配置已变化，请重新分析。";
    }

    /// <summary>卸载只撤销结果归属和前台等待；后台文件调用实际退出前不启动另一次分析。</summary>
    public void SuspendAnalysis()
    {
        _analysisDispatcher.VerifyAccess();
        _analysisSuspended = true;
        ++_analysisGeneration;
        CancelAnalysisWait();
        OnPropertyChanged(nameof(CanAnalyze));
        OnPropertyChanged(nameof(HasSuggestion));
    }

    /// <summary>重新进入页面时丢弃旧建议；仍未退出的分析继续保留并收尾。</summary>
    public void ResumeAnalysis()
    {
        _analysisDispatcher.VerifyAccess();
        if (!_analysisSuspended) return;
        _analysisSuspended = false;
        InvalidateAnalysis();
        OnPropertyChanged(nameof(CanAnalyze));
    }

    private void CancelAnalysisWait()
    {
        if (_activeAnalysis is not { CancelRequested: false } operation) return;
        operation.CancelRequested = true;
        // A worker callback can delay token callback delivery to WaitAsync. End
        // the editor wait independently, without claiming the worker has exited.
        operation.WaitCancelled.TrySetResult();
        // Cancellation callbacks may themselves block. CancelAsync does not run
        // those callbacks inline on the editor Dispatcher.
        operation.CancellationCompletion = operation.Cancellation.CancelAsync();
    }

    private bool IsCurrentAnalysis(AnalysisOperation operation) => !_analysisSuspended &&
        operation.Generation == _analysisGeneration && ReferenceEquals(Setting, operation.Setting) &&
        !operation.Token.IsCancellationRequested;

    /// <summary>是否正在分析固件</summary>
    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set { if (_isAnalyzing == value) return; _isAnalyzing = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanAnalyze)); }
    }

    public bool CanAnalyze => !_analysisSuspended && !_isAnalyzing;

    /// <summary>分析结果概述</summary>
    public string AnalysisMessage
    {
        get => _analysisMessage;
        private set { if (_analysisMessage == value) return; _analysisMessage = value; OnPropertyChanged(); }
    }

    /// <summary>分析出的数据段明细</summary>
    public ObservableCollection<string> AnalysisSegments { get; } = [];

    /// <summary>推导出的地址与长度格式标识建议值</summary>
    public string SuggestedAlfid
    {
        get => _suggestedAlfid;
        private set { if (_suggestedAlfid == value) return; _suggestedAlfid = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSuggestion)); }
    }

    public bool HasSuggestion => !_analysisSuspended && !string.IsNullOrEmpty(_suggestedAlfid);

    /// <summary>解析当前配置的固件文件并刷新分析结果</summary>
    public async Task AnalyzeFirmwareAsync()
    {
        _analysisDispatcher.VerifyAccess();
        if (Setting == null || !CanAnalyze)
            return;

        var operation = new AnalysisOperation(Setting, ++_analysisGeneration);
        operation.Token = operation.Cancellation.Token;
        // Capture immutable inputs before any property notification can refresh
        // the page or replace its Setting.
        string path = Setting.FilePath, baseAddress = Setting.BaseAddress;
        var format = Setting.Format;
        _activeAnalysis = operation;
        IsAnalyzing = true;
        if (IsCurrentAnalysis(operation)) AnalysisMessage = "正在解析固件文件...";
        if (IsCurrentAnalysis(operation))
        {
            _segmentNotificationDepth++;
            try { AnalysisSegments.Clear(); }
            finally { _segmentNotificationDepth--; }
        }
        if (IsCurrentAnalysis(operation)) SuggestedAlfid = string.Empty;

        if (IsCurrentAnalysis(operation))
            operation.Worker = Task.Run(() =>
            {
                operation.Token.ThrowIfCancellationRequested();
                return _analyze(path, format, baseAddress, operation.Token);
            }, CancellationToken.None);
        // This owns the actual worker and cancellation callbacks independently of
        // the button handler's cancellable wait.
        _analysisCompletion = ObserveAnalysisExitAsync(operation);

        try
        {
            var completed = await Task.WhenAny(operation.Worker, operation.WaitCancelled.Task);
            if (completed != operation.Worker || !IsCurrentAnalysis(operation)) return;
            var result = await operation.Worker;
            if (!IsCurrentAnalysis(operation)) return;

            AnalysisMessage = result.Success
                ? $"{result.Message}\n地址范围 0x{result.MinAddress:X8} - 0x{result.MaxAddress:X8}"
                : result.Message;

            foreach (var detail in result.SegmentDetails)
            {
                if (!IsCurrentAnalysis(operation)) return;
                _segmentNotificationDepth++;
                try { AnalysisSegments.Add(detail); }
                finally { _segmentNotificationDepth--; }
            }

            if (result.Success && IsCurrentAnalysis(operation))
                SuggestedAlfid = result.SuggestedAddressAndLengthFormatId;
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (IsCurrentAnalysis(operation)) AnalysisMessage = $"分析失败：{ex.Message}";
        }
    }

    private async Task ObserveAnalysisExitAsync(AnalysisOperation operation)
    {
        try { await operation.Worker.ConfigureAwait(false); }
        catch (Exception) { /* The foreground observes current failures; stale failures cannot publish. */ }
        await _analysisDispatcher.InvokeAsync(async () =>
        {
            try { await operation.CancellationCompletion; }
            catch (Exception ex)
            {
                if (IsCurrentAnalysis(operation)) AnalysisMessage = $"分析取消收尾失败：{ex.Message}";
            }
            finally
            {
                operation.Cancellation.Dispose();
                if (ReferenceEquals(_activeAnalysis, operation))
                {
                    _activeAnalysis = null;
                    IsAnalyzing = false;
                }
            }
        }).Task.Unwrap().ConfigureAwait(false);
    }

    /// <summary>将分析得到的格式标识建议值写入设置</summary>
    public void ApplySuggestedAlfid()
    {
        if (!HasSuggestion)
            return;

        AddressAndLengthFormatId = $"\"{SuggestedAlfid}\"";
    }

    // ── ECU 刷写规范预设 ────────────────────────────────────────────
    private EcuFlashPreset? _selectedPreset;

    /// <summary>已保存的预设列表</summary>
    public ObservableCollection<EcuFlashPreset> Presets { get; } = [];

    public EcuFlashPreset? SelectedPreset
    {
        get => _selectedPreset;
        set { if (ReferenceEquals(_selectedPreset, value)) return; _selectedPreset = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelectedPreset)); }
    }

    public bool HasSelectedPreset => _selectedPreset != null;

    private string _presetMessage = string.Empty;
    public string PresetMessage
    {
        get => _presetMessage;
        private set { _presetMessage = value; OnPropertyChanged(); }
    }

    /// <summary>从本地重新加载预设列表</summary>
    public void ReloadPresets()
    {
        var name = _selectedPreset?.Name;
        try
        {
            var presets = EcuFlashPresetStore.Load(_presetPath);
            Presets.Clear();
            foreach (var preset in presets) Presets.Add(preset);
            SelectedPreset = name == null ? null : Presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            PresetMessage = string.Empty;
        }
        catch (Exception ex) { PresetMessage = $"加载刷写预设失败，原文件保留：{ex.Message}"; }
    }

    /// <summary>把选中的预设应用到当前设置</summary>
    public void ApplySelectedPreset()
    {
        if (Setting == null || SelectedPreset is not { } preset)
            return;

        AddressAndLengthFormatId = preset.AddressAndLengthFormatId;
        DataFormatId = preset.DataFormatId;
        EraseBeforeDownload = preset.EraseBeforeDownload;
        EraseRoutineId = preset.EraseRoutineId;
        EraseWithAddressAndLength = preset.EraseWithAddressAndLength;
        EraseTimeoutMs = preset.EraseTimeoutMs;
        MaxBlockSize = preset.MaxBlockSize;
        PreDownloadDelayMs = preset.PreDownloadDelayMs;
        BlockRetryCount = preset.BlockRetryCount;
        InterBlockDelayMs = preset.InterBlockDelayMs;
        CheckModeIndex = (int)preset.CheckMode;
        CheckRoutineId = preset.CheckRoutineId;
    }

    /// <summary>把当前设置保存为指定名称的预设（同名覆盖）</summary>
    public void SaveAsPreset(string name, string remark)
    {
        if (Setting == null || string.IsNullOrWhiteSpace(name))
            return;

        try
        {
        EcuFlashPresetStore.Upsert(_presetPath, new EcuFlashPreset
        {
            Name = name.Trim(),
            Remark = remark ?? string.Empty,
            AddressAndLengthFormatId = Setting.AddressAndLengthFormatId,
            DataFormatId = Setting.DataFormatId,
            EraseBeforeDownload = Setting.EraseBeforeDownload,
            EraseRoutineId = Setting.EraseRoutineId,
            EraseWithAddressAndLength = Setting.EraseWithAddressAndLength ?? true,
            EraseTimeoutMs = Setting.EraseTimeoutMs,
            MaxBlockSize = Setting.MaxBlockSize,
            PreDownloadDelayMs = Setting.PreDownloadDelayMs,
            BlockRetryCount = Setting.BlockRetryCount,
            InterBlockDelayMs = Setting.InterBlockDelayMs,
            CheckMode = Setting.CheckMode,
            CheckRoutineId = Setting.CheckRoutineId
        });

        ReloadPresets();
        SelectedPreset = Presets.FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) { PresetMessage = $"保存刷写预设失败，未覆盖原文件：{ex.Message}"; }
    }

    /// <summary>删除当前选中的预设</summary>
    public void DeleteSelectedPreset()
    {
        if (SelectedPreset is not { } preset)
            return;

        try
        {
            EcuFlashPresetStore.Delete(_presetPath, preset.Name);
            SelectedPreset = null;
            ReloadPresets();
        }
        catch (Exception ex) { PresetMessage = $"删除刷写预设失败，原文件保留：{ex.Message}"; }
    }
}
