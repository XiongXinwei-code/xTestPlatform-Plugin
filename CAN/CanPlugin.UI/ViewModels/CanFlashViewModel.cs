using System.Collections.ObjectModel;
using CAN.Flash.Models;
using CAN.UI.Models;
using CAN.UI.Services;

namespace CAN.UI.ViewModels;

/// <summary>UDS 固件烧录编辑器 ViewModel</summary>
public sealed class CanFlashViewModel : UdsViewModelBase<CanFlashSetting>
{
    public CanFlashViewModel()
    {
        ReloadPresets();
    }

    // ── 固件文件 ────────────────────────────────────────────────────
    public string FilePath
    {
        get => Setting?.FilePath ?? "";
        set { if (Setting == null || Setting.FilePath == value) return; Setting.FilePath = value; OnPropertyChanged(); QueueSave(); }
    }

    public int FormatIndex
    {
        get => (int)(Setting?.Format ?? FirmwareFormat.Auto);
        set { if (Setting == null) return; Setting.Format = (FirmwareFormat)value; OnPropertyChanged(); OnPropertyChanged(nameof(IsBinaryFormat)); QueueSave(); }
    }

    /// <summary>基地址仅在 Binary 格式下有意义</summary>
    public bool IsBinaryFormat => Setting?.Format == FirmwareFormat.Binary;

    public string BaseAddress
    {
        get => Setting?.BaseAddress ?? "";
        set { if (Setting == null || Setting.BaseAddress == value) return; Setting.BaseAddress = value; OnPropertyChanged(); QueueSave(); }
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

    /// <summary>是否正在分析固件</summary>
    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set { if (_isAnalyzing == value) return; _isAnalyzing = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanAnalyze)); }
    }

    public bool CanAnalyze => !_isAnalyzing;

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

    public bool HasSuggestion => !string.IsNullOrEmpty(_suggestedAlfid);

    /// <summary>解析当前配置的固件文件并刷新分析结果</summary>
    public async Task AnalyzeFirmwareAsync()
    {
        if (Setting == null || IsAnalyzing)
            return;

        IsAnalyzing = true;
        AnalysisMessage = "正在解析固件文件...";
        AnalysisSegments.Clear();
        SuggestedAlfid = string.Empty;

        try
        {
            var result = await FirmwareAnalyzer.AnalyzeAsync(Setting.FilePath, Setting.Format, Setting.BaseAddress);

            AnalysisMessage = result.Success
                ? $"{result.Message}\n地址范围 0x{result.MinAddress:X8} - 0x{result.MaxAddress:X8}"
                : result.Message;

            foreach (var detail in result.SegmentDetails)
                AnalysisSegments.Add(detail);

            if (result.Success)
                SuggestedAlfid = result.SuggestedAddressAndLengthFormatId;
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    /// <summary>将分析得到的格式标识建议值写入设置</summary>
    public void ApplySuggestedAlfid()
    {
        if (string.IsNullOrEmpty(SuggestedAlfid))
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

    /// <summary>从本地重新加载预设列表</summary>
    public void ReloadPresets()
    {
        var name = _selectedPreset?.Name;
        Presets.Clear();
        foreach (var preset in EcuFlashPresetStore.Load())
            Presets.Add(preset);

        SelectedPreset = name == null
            ? null
            : Presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
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
        EraseTimeoutMs = preset.EraseTimeoutMs;
        MaxBlockSize = preset.MaxBlockSize;
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

        EcuFlashPresetStore.Upsert(new EcuFlashPreset
        {
            Name = name.Trim(),
            Remark = remark ?? string.Empty,
            AddressAndLengthFormatId = Setting.AddressAndLengthFormatId,
            DataFormatId = Setting.DataFormatId,
            EraseBeforeDownload = Setting.EraseBeforeDownload,
            EraseRoutineId = Setting.EraseRoutineId,
            EraseTimeoutMs = Setting.EraseTimeoutMs,
            MaxBlockSize = Setting.MaxBlockSize,
            BlockRetryCount = Setting.BlockRetryCount,
            InterBlockDelayMs = Setting.InterBlockDelayMs,
            CheckMode = Setting.CheckMode,
            CheckRoutineId = Setting.CheckRoutineId
        });

        ReloadPresets();
        SelectedPreset = Presets.FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>删除当前选中的预设</summary>
    public void DeleteSelectedPreset()
    {
        if (SelectedPreset is not { } preset)
            return;

        EcuFlashPresetStore.Delete(preset.Name);
        SelectedPreset = null;
        ReloadPresets();
    }
}
