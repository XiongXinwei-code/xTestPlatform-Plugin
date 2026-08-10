using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UdpCommunication.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.UI.ViewModels;

/// <summary>
/// 表示一个 UDP_Open 步骤的引用项（用于下拉框）。
/// </summary>
public sealed class UdpOpenOption
{
    /// <summary>唯一标识：引用的 UDP_Open 步骤的 StepAddress（运行时与编辑器均使用）。</summary>
    public string StepAddress { get; init; } = string.Empty;

    /// <summary>本地 IP 地址（用于展示）。</summary>
    public string LocalAddress { get; init; } = string.Empty;

    /// <summary>本地端口（用于展示）。</summary>
    public int LocalPort { get; init; }

    /// <summary>步骤所在 Sequence 名称（用于展示）。</summary>
    public string SequenceName { get; init; } = string.Empty;

    /// <summary>用户自定义的步骤描述（用于展示）。</summary>
    public string StepDescription { get; init; } = string.Empty;

    public string Display
    {
        get
        {
            var address = string.IsNullOrEmpty(LocalAddress) ? "(未配置)" : $"{LocalAddress}:{LocalPort}";
            if (string.IsNullOrEmpty(StepDescription))
            {
                return $"{SequenceName}  →  {address}";
            }
            return $"{SequenceName}  ·  {StepDescription}  →  {address}";
        }
    }
}

public abstract class UdpViewModelBase : INotifyPropertyChanged
{
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    protected bool _suppressSave;
    protected Step? _step;
    protected IStepSettingSerializer? _serializer;

    /// <summary>当前 TestPlan 中所有 UDP_Open 步骤的引用项（用于 OpenStepAddress 下拉框）。</summary>
    public ObservableCollection<UdpOpenOption> AvailableOpenSteps { get; } = new();

    /// <summary>当前 TestPlan（编辑器上下文）。由 View 在构造时注入。</summary>
    public SequenceFile? SequenceFile { get; set; }

    private UdpOpenOption? _selectedOpenStep;
    public UdpOpenOption? SelectedOpenStep
    {
        get => _selectedOpenStep;
        set
        {
            if (ReferenceEquals(_selectedOpenStep, value)) return;
            _selectedOpenStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConnectedEndpointHint));
            OnSelectedOpenStepChanged(value);
        }
    }

    /// <summary>当前选中 OpenStepAddress 所对应的连接提示文本（IP:Port）。</summary>
    public string ConnectedEndpointHint
    {
        get
        {
            var selected = SelectedOpenStep;
            if (selected == null) return "（未选择 UDP_Open 步骤）";
            return $"已连接：{selected.LocalAddress}:{selected.LocalPort}";
        }
    }

    /// <summary>子类在 SelectedOpenStep 变化后应把 OpenStepAddress 写回自己的 Setting 对象。</summary>
    protected abstract void OnSelectedOpenStepChanged(UdpOpenOption? option);

    /// <summary>加载当前测试计划中所有 UDP_Open 步骤到 AvailableOpenSteps，并设置 SelectedOpenStep 为当前配置。</summary>
    protected void RefreshAvailableOpenSteps(string currentOpenStepAddress)
    {
        AvailableOpenSteps.Clear();
        if (SequenceFile != null)
        {
            foreach (var seq in SequenceFile.Sequences.Values)
            {
                foreach (var block in seq.StepItems.Values)
                {
                    foreach (var step in block)
                    {
                        if (step.StepSetting?.StepType != UdpOpenPlugin.StepTypeIdConst) continue;
                        if (step.StepSetting.Setting is not { Length: > 0 } data) continue;
                        if (string.IsNullOrEmpty(step.StepSetting.StepAddress)) continue;

                        UdpOpenSetting? setting = null;
                        try
                        {
                            setting = (UdpOpenSetting?)new UdpOpenPlugin().CreateSerializer()
                                .Deserialize(data, step.StepSetting.SettingVersion);
                        }
                        catch
                        {
                            continue;
                        }
                        if (setting == null) continue;

                        AvailableOpenSteps.Add(new UdpOpenOption
                        {
                            StepAddress = step.StepSetting.StepAddress,
                            LocalAddress = setting.LocalAddress.Trim('"'),
                            LocalPort = setting.LocalPort,
                            SequenceName = seq.SequenceName,
                            StepDescription = step.PropertiesSetting?.General?.StepDescription ?? string.Empty
                        });
                    }
                }
            }
        }

        SelectedOpenStep = AvailableOpenSteps.FirstOrDefault(o => o.StepAddress == currentOpenStepAddress);
        if (SelectedOpenStep == null && AvailableOpenSteps.Count > 0)
        {
            // 在编辑器阶段主动尝试把空配置预填为第一个 Open 步骤，方便用户使用。
            SelectedOpenStep = AvailableOpenSteps[0];
        }
    }

    public void AttachSerializer(IStepSettingSerializer s)
    {
        _serializer = s;
        if (_step != null) Load();
    }

    public void AttachStep(Step step)
    {
        _step = step;
        Load();
    }

    protected abstract void Load();

    protected void QueueSave()
    {
        if (_suppressSave || _step == null || _serializer == null) return;
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        var setting = GetSetting();
        if (setting == null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SaveDebounceMs, cts.Token);
                _step.StepSetting.Setting = _serializer.Serialize(setting);
            }
            catch (TaskCanceledException) { }
        });
    }

    public void FlushPendingChanges()
    {
        if (_suppressSave || _step == null || _serializer == null) return;
        _saveCts?.Cancel();
        var setting = GetSetting();
        if (setting == null) return;
        _step.StepSetting.Setting = _serializer.Serialize(setting);
    }

    protected abstract object? GetSetting();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
