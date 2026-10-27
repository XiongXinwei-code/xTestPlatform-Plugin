using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CAN.CANopen.Cyclic;
using CAN.Runtime;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.ViewModels;

public sealed class CyclicServiceViewModel(CyclicPluginBase plugin) : INotifyPropertyChanged
{
    private CyclicSetting _setting = new();
    private Step? _step;
    private readonly PluginCommon.EditorSaveScheduler _save = new();
    public void Flush() => _save.Flush();
    public bool EnableMonitoringSync { get => _setting.EnableMonitoringSync; set { if (Equals(_setting.EnableMonitoringSync, value)) return; _setting.EnableMonitoringSync = value; Changed(); Save(); } }
    public int MonitoringPeriodMs { get => _setting.MonitoringPeriodMs; set { if (Equals(_setting.MonitoringPeriodMs, value)) return; _setting.MonitoringPeriodMs = value; Changed(); Save(); } }
    public bool ResynchronizeLateMonitoring { get => _setting.ResynchronizeLateMonitoring; set { if (Equals(_setting.ResynchronizeLateMonitoring, value)) return; _setting.ResynchronizeLateMonitoring = value; Changed(); Save(); } }
    public int MonitoringConsecutiveLatenessLimit { get => _setting.MonitoringConsecutiveLatenessLimit; set { if (Equals(_setting.MonitoringConsecutiveLatenessLimit, value)) return; _setting.MonitoringConsecutiveLatenessLimit = value; Changed(); Save(); } }
    public bool SuperviseFeedback { get => _setting.SuperviseFeedback; set { if (Equals(_setting.SuperviseFeedback, value)) return; _setting.SuperviseFeedback = value; Changed(); Save(); } }
    public int FeedbackStartupGraceMs { get => _setting.FeedbackStartupGraceMs; set { if (Equals(_setting.FeedbackStartupGraceMs, value)) return; _setting.FeedbackStartupGraceMs = value; Changed(); Save(); } }
    public bool UseFiniteTrajectory { get => _setting.UseFiniteTrajectory; set { if (Equals(_setting.UseFiniteTrajectory, value)) return; _setting.UseFiniteTrajectory = value; Changed(); Save(); } }
    public int TrajectoryDurationMs { get => _setting.TrajectoryDurationMs; set { if (Equals(_setting.TrajectoryDurationMs, value)) return; _setting.TrajectoryDurationMs = value; Changed(); Save(); } }
    public TrajectoryMissPolicy MissPolicy { get => _setting.MissPolicy; set { if (_setting.MissPolicy == value) return; _setting.MissPolicy = value; Changed(); Save(); } }
    public int LatenessWindowMs { get => _setting.LatenessWindowMs; set { if (_setting.LatenessWindowMs == value) return; _setting.LatenessWindowMs = value; Changed(); Save(); } }
    public int MaxSkippedPoints { get => _setting.MaxSkippedPoints; set { if (_setting.MaxSkippedPoints == value) return; _setting.MaxSkippedPoints = value; Changed(); Save(); } }
    public int HoldTimeoutMs { get => _setting.HoldTimeoutMs; set { if (_setting.HoldTimeoutMs == value) return; _setting.HoldTimeoutMs = value; Changed(); Save(); } }
    public string TrajectoryJson { get => _setting.TrajectoryJson; set { if (Equals(_setting.TrajectoryJson, value)) return; _setting.TrajectoryJson = value; Changed(); Save(); } }
    public string StopCommandsJson { get => _setting.StopCommandsJson; set { if (Equals(_setting.StopCommandsJson, value)) return; _setting.StopCommandsJson = value; Changed(); Save(); } }
    public ObservableCollection<CyclicRowViewModel> Routes { get; } = [];
    public ObservableCollection<CyclicRowViewModel> Targets { get; } = [];
    public Action<string, Action>? ExecuteCommand { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public string ConnectionName { get => _setting.ConnectionName; set { if (Equals(_setting.ConnectionName, value)) return; _setting.ConnectionName = value; Changed(); Save(); } }
    public string ServiceName { get => _setting.ServiceName; set { if (Equals(_setting.ServiceName, value)) return; _setting.ServiceName = value; Changed(); Save(); } }
    public string NodeIds { get => _setting.NodeIds; set { if (Equals(_setting.NodeIds, value)) return; _setting.NodeIds = value; Changed(); Save(); } }
    public string SyncCobId { get => _setting.SyncCobId; set { if (Equals(_setting.SyncCobId, value)) return; _setting.SyncCobId = value; Changed(); Save(); } }
    public string SyncData { get => _setting.SyncData; set { if (Equals(_setting.SyncData, value)) return; _setting.SyncData = value; Changed(); Save(); } }
    public int PeriodMs { get => _setting.PeriodMs; set { if (Equals(_setting.PeriodMs, value)) return; _setting.PeriodMs = value; Changed(); Save(); } }
    public int CycleTimeoutMs { get => _setting.CycleTimeoutMs; set { if (Equals(_setting.CycleTimeoutMs, value)) return; _setting.CycleTimeoutMs = value; Changed(); Save(); } }
    public int MaxLatenessMs { get => _setting.MaxLatenessMs; set { if (Equals(_setting.MaxLatenessMs, value)) return; _setting.MaxLatenessMs = value; Changed(); Save(); } }
    public int InputMaxAgeMs { get => _setting.InputMaxAgeMs; set { if (Equals(_setting.InputMaxAgeMs, value)) return; _setting.InputMaxAgeMs = value; Changed(); Save(); } }
    public int StopTimeoutMs { get => _setting.StopTimeoutMs; set { if (Equals(_setting.StopTimeoutMs, value)) return; _setting.StopTimeoutMs = value; Changed(); Save(); } }
    public bool RequireOnBusConfirmation { get => _setting.RequireOnBusConfirmation; set { if (Equals(_setting.RequireOnBusConfirmation, value)) return; _setting.RequireOnBusConfirmation = value; Changed(); Save(); } }
    public bool RequireAllValid { get => _setting.RequireAllValid; set { if (Equals(_setting.RequireAllValid, value)) return; _setting.RequireAllValid = value; Changed(); Save(); } }
    public string ResultVariable { get => _setting.ResultVariable; set { if (Equals(_setting.ResultVariable, value)) return; _setting.ResultVariable = value; Changed(); Save(); } }
    public void Load(Step step)
    {
        Flush();
        foreach (var row in Routes.Concat(Targets)) row.Save = null;
        _step = null;
        Routes.Clear(); Targets.Clear();
        var serializer = plugin.CreateSerializer();
        _setting = step.StepSetting.Setting is { Length: > 0 }
            ? (CyclicSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)
            : new CyclicSetting();
        foreach (var r in _setting.FeedbackRoutes)
            Routes.Add(new() { Name = r.Name, CobId = r.CobId, Dlc = r.Dlc, MaxAgeMs = r.MaxAgeMs, Save = Save });
        foreach (var r in _setting.Targets)
            Targets.Add(new() { CobId = r.CobId, Dlc = r.Dlc, Data = r.Data, Save = Save });
        _step = step;
        Changed("");
    }
    public void AddRoute() { Routes.Add(new() { Save = Save }); Save(); }
    public void AddTarget() { Targets.Add(new() { CobId = "0x201", Dlc = 4, Save = Save }); Save(); }
    public void RemoveRoute(CyclicRowViewModel row) { row.Save = null; Routes.Remove(row); Save(); }
    public void RemoveTarget(CyclicRowViewModel row) { row.Save = null; Targets.Remove(row); Save(); }
    public void Save()
    {
        if (_step != null) _save.Queue(SaveCore);
    }
    private void SaveCore()
    {
        if (_step == null) return;
        _setting.FeedbackRoutes = Routes.Select(r => new FeedbackRouteSetting { Name = r.Name, CobId = r.CobId, Dlc = r.Dlc, MaxAgeMs = r.MaxAgeMs }).ToList();
        _setting.Targets = Targets.Select(r => new TargetFrameSetting { CobId = r.CobId, Data = r.Data, Dlc = r.Dlc }).ToList();
        var serializer = plugin.CreateSerializer();
        var bytes = serializer.Serialize(_setting);
        var step = _step;
        void Apply() { step.StepSetting.Setting = bytes; step.StepSetting.SettingVersion = serializer.SettingVersion; }
        if (ExecuteCommand != null) ExecuteCommand("修改 CANopen 通信服务配置", Apply); else Apply();
    }
}

public sealed class CyclicRowViewModel : INotifyPropertyChanged
{
    public Action? Save { get; set; }
    private string _name = "TPDO", _cobId = "0x181", _data = "\"00 00 00 00\"";
    private int _dlc = 8, _maxAgeMs = 100;
    public string Name { get => _name; set => Set(ref _name, value); }
    public string CobId { get => _cobId; set => Set(ref _cobId, value); }
    public string Data { get => _data; set => Set(ref _data, value); }
    public int Dlc { get => _dlc; set => Set(ref _dlc, value); }
    public int MaxAgeMs { get => _maxAgeMs; set => Set(ref _maxAgeMs, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value; PropertyChanged?.Invoke(this, new(name)); Save?.Invoke();
    }
}
