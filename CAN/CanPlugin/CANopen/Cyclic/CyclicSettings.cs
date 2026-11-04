using MessagePack;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using xTestPlatform.Core.Models.StepSettings;
using CAN.Runtime;

namespace CAN.CANopen.Cyclic;

public enum CyclicOperation { ServiceStart, CycleStart, CycleInputsUpdate, SnapshotRead, CycleStop, ServiceStop }

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class CyclicSetting
{
    [ExpressionField] public string ConnectionName { get; set; } = "\"CAN1\"";
    [ExpressionField] public string ServiceName { get; set; } = "\"Feedback1\"";
    public ObservableCollection<FeedbackRouteSetting> FeedbackRoutes { get; set; } = [];
    public ObservableCollection<TargetFrameSetting> Targets { get; set; } = [];
    /// <summary>序列求值为逗号分隔节点列表；只用于控制占用，不修改设备配置。</summary>
    [ExpressionField] public string NodeIds { get; set; } = "\"1\"";
    /// <summary>当前步骤的 SYNC 配置：ServiceStart 为背景，CycleStart 为指令；二者独立。</summary>
    [ExpressionField] public string SyncCobId { get; set; } = "0x80";
    [ExpressionField] public string SyncData { get; set; } = "\"\"";
    public int PeriodMs { get; set; } = 10;
    public int CycleTimeoutMs { get; set; } = 1000;
    public int MaxLatenessMs { get; set; } = 100;
    public int InputMaxAgeMs { get; set; } = 1000;
    public int StopTimeoutMs { get; set; } = 5000;
    public bool EnableMonitoringSync { get; set; }
    /// <summary>背景 SYNC 独立起始间隔；目标 RPDO 后的额外 SYNC 不重置本周期。</summary>
    public int MonitoringPeriodMs { get; set; } = 10;
    public bool ResynchronizeLateMonitoring { get; set; } = true;
    public int MonitoringConsecutiveLatenessLimit { get; set; } = 3;
    public bool SuperviseFeedback { get; set; }
    public int FeedbackStartupGraceMs { get; set; } = 1000;
    public bool UseFiniteTrajectory { get; set; }
    public int TrajectoryDurationMs { get; set; } = 1000;
    public TrajectoryMissPolicy MissPolicy { get; set; } = TrajectoryMissPolicy.SkipAndCatchUp;
    public int LatenessWindowMs { get; set; }
    public int MaxSkippedPoints { get; set; } = 20;
    public int HoldTimeoutMs { get; set; } = 100;
    [ExpressionField] public string TrajectoryJson { get; set; } = "\"[]\"";
    [ExpressionField] public string StopCommandsJson { get; set; } = "\"[]\"";
    /// <summary>默认仅提交；true为独占逐批确认诊断，不能与其它发送源混跑。</summary>
    public bool RequireOnBusConfirmation { get; set; }
    public bool RequireAllValid { get; set; }
    /// <summary>已声明 string 变量，输出完整 JSON 快照，不依赖 SDK 任意 DTO 赋值能力。</summary>
    [VariablePathField] public string ResultVariable { get; set; } = "";
}

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class FeedbackRouteSetting : INotifyPropertyChanged
{
    public override string ToString() => CAN.Helpers.CanSettingText.Format(this);
    private string _name = "TPDO", _cobId = "0x181";
    private int _dlc = 8, _maxAgeMs = 100;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    [ExpressionField] public string CobId { get => _cobId; set => SetProperty(ref _cobId, value); }
    public int Dlc { get => _dlc; set => SetProperty(ref _dlc, value); }
    public int MaxAgeMs { get => _maxAgeMs; set => SetProperty(ref _maxAgeMs, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new(name));
    }
}

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class TargetFrameSetting : INotifyPropertyChanged
{
    public override string ToString() => CAN.Helpers.CanSettingText.Format(this);
    private string _cobId = "0x201", _data = "\"00 00 00 00\"";
    private int _dlc = 4;
    [ExpressionField] public string CobId { get => _cobId; set => SetProperty(ref _cobId, value); }
    [ExpressionField] public string Data { get => _data; set => SetProperty(ref _data, value); }
    public int Dlc { get => _dlc; set => SetProperty(ref _dlc, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new(name));
    }
}
