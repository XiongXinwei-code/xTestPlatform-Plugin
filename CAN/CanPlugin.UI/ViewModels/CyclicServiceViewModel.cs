using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    private Step? _lastAttachedStep;
    private bool _active;
    private ObservableCollection<FeedbackRouteSetting>? _observedRoutes;
    private ObservableCollection<TargetFrameSetting>? _observedTargets;
    private readonly HashSet<INotifyPropertyChanged> _observedItems = [];
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
    public ObservableCollection<FeedbackRouteSetting> Routes => _setting.FeedbackRoutes;
    public ObservableCollection<TargetFrameSetting> Targets => _setting.Targets;
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
        _active = false;
        DetachCollections();
        _step = null; _lastAttachedStep = step;
        var serializer = plugin.CreateSerializer();
        _setting = step.StepSetting.Setting is { Length: > 0 }
            ? (CyclicSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)
            : new CyclicSetting();
        _observedRoutes = Routes;
        _observedTargets = Targets;
        _observedRoutes.CollectionChanged += OnCollectionChanged;
        _observedTargets.CollectionChanged += OnCollectionChanged;
        ObserveCurrentItems();
        _step = step;
        _active = true;
        Changed("");
    }
    public void Activate()
    {
        if (!_active && _lastAttachedStep is { } step) Load(step);
    }
    public void Deactivate()
    {
        Flush();
        _active = false; _step = null;
        DetachCollections();
    }
    public void AddRoute() { if (_active) Routes.Add(new()); }
    public void AddTarget() { if (_active) Targets.Add(new()); }
    public void RemoveRoute(FeedbackRouteSetting row) { if (_active) Routes.Remove(row); }
    public void RemoveTarget(TargetFrameSetting row) { if (_active) Targets.Remove(row); }
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_active || (!ReferenceEquals(sender, _observedRoutes) && !ReferenceEquals(sender, _observedTargets))) return;
        ObserveCurrentItems();
        Save();
    }
    private void ObserveCurrentItems()
    {
        // Reconcile membership for Replace/Reset and shared item instances as well
        // as Add/Remove. Removing one duplicate must not detach a still-bound row.
        var current = Routes.Cast<INotifyPropertyChanged>().Concat(Targets).ToHashSet();
        foreach (var item in _observedItems.Except(current).ToArray())
        { item.PropertyChanged -= OnItemChanged; _observedItems.Remove(item); }
        foreach (var item in current)
            if (_observedItems.Add(item)) item.PropertyChanged += OnItemChanged;
    }
    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_active && sender is INotifyPropertyChanged item && _observedItems.Contains(item)) Save();
    }
    private void DetachCollections()
    {
        if (_observedRoutes != null) _observedRoutes.CollectionChanged -= OnCollectionChanged;
        if (_observedTargets != null) _observedTargets.CollectionChanged -= OnCollectionChanged;
        foreach (var item in _observedItems) item.PropertyChanged -= OnItemChanged;
        _observedItems.Clear(); _observedRoutes = null; _observedTargets = null;
    }
    public void Save()
    {
        if (!_active || _step is not { } step) return;
        var setting = _setting;
        var serializer = plugin.CreateSerializer();
        var command = ExecuteCommand;
        _save.Queue(() =>
        {
            var bytes = serializer.Serialize(setting);
            void Apply() { step.StepSetting.Setting = bytes; step.StepSetting.SettingVersion = serializer.SettingVersion; }
            if (command != null) command("修改 CANopen 通信服务配置", Apply); else Apply();
        });
    }
}
