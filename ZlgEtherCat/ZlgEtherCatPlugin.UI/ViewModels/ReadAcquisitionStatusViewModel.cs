using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ReadAcquisitionStatusViewModel : ZlgViewModelBase<ReadAcquisitionStatusSetting>
{
    public ReadAcquisitionStatusOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ReadAcquisitionStatusOperation, string>> OperationChoices => [new(ReadAcquisitionStatusOperation.ReadAcquisitionStatus, "读取采集状态")];
    public string OperationTitle => Operation switch
    {
        ReadAcquisitionStatusOperation.ReadAcquisitionStatus => "读取采集状态",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ReadAcquisitionStatusOperation.ReadAcquisitionStatus => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n RxQueue 为最后一次观测值，须同时查看 ObservationAgeMs 与 MaxPollGapMs；SDK 阻塞时监控也会延迟。LastObservationTimestamp/LastRecoveryTimestamp 为主机队列观测/恢复时刻的单调时钟刻度；HighWatermarkEvents/BacklogWarningEvents 计告警事件段，FullQueueObservations 计满深度观测次数。高水位/持续积压状态可随观测变化，OverflowSuspected 为疑似事件锁存，HardwareDroppedFrames=null。Queue 容量未知时仍记录深度/持续积压，但禁用高水位和满判定；非 Queue 模式禁用队列告警。告警由既有后台工作循环发送，不在 SDK 工作线程中调用宿主日志；重复消息受 RxAlarmRepeatMs 节流并可合并，NotificationError 显示通知失败。 本操作只读缓存，不调用 SDK；SDK 阻塞或通道隔离时仍可查看最后观测和错误，直到连接关闭。\n返回：对象：Running、Sequence、AgeMs、QueueBacklog、LocallyValid、Error、WorkerStarts、Priority（Requested 为 AcquisitionPriority?，Actual/RestoreTarget 为可空的 .NET 线程优先级名称，Error 为可空的设置或恢复错误）、RxQueue（Available、Monitoring、Capacity、CurrentDepth、PeakDepth、HighWatermarkPercent、HighWatermarkDepth、BacklogWarningMs、AlarmRepeatMs、LatestOnly、HighWatermarkActive、BacklogWarningActive、OverflowSuspected、HighWatermarkEvents、BacklogWarningEvents、FullQueueObservations、LastObservationTimestamp、ObservationAgeMs、MaxPollGapMs、LastRecoveryTimestamp、LastAlarm、NotificationError、HardwareDroppedFrames=null、HardwareDroppedFramesAvailable=false）、TimestampSource、TimestampFrequencyHz、HardwareTimestampAvailable=false、HardwareTimestamp=null、HardwareSampleAgeMs=null；Sequence 是 long 软件序号，AgeMs 为主机读取完成后的年龄，不含此前 RX 排队时间。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ReadAcquisitionStatusSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ReadAcquisitionStatusOperation.ReadAcquisitionStatus => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ReadAcquisitionStatusOperation.ReadAcquisitionStatus => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ReadAcquisitionStatusSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ReadAcquisitionStatusOperation.ReadAcquisitionStatus => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ReadAcquisitionStatusOperation.ReadAcquisitionStatus => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ReadAcquisitionStatusSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ReadAcquisitionStatusOperation.ReadAcquisitionStatus => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ReadAcquisitionStatusOperation.ReadAcquisitionStatus => "可选已声明变量完整路径；对象：Running、Sequence、AgeMs、QueueBacklog、LocallyValid、Error、WorkerStarts、Priority（Requested 为 AcquisitionPriority?，Actual/RestoreTarget 为可空的 .NET 线程优先级名称，Error 为可空的设置或恢复错误）、RxQueue（Available、Monitoring、Capacity、CurrentDepth、PeakDepth、HighWatermarkPercent、HighWatermarkDepth、BacklogWarningMs、AlarmRepeatMs、LatestOnly、HighWatermarkActive、BacklogWarningActive、OverflowSuspected、HighWatermarkEvents、BacklogWarningEvents、FullQueueObservations、LastObservationTimestamp、ObservationAgeMs、MaxPollGapMs、LastRecoveryTimestamp、LastAlarm、NotificationError、HardwareDroppedFrames=null、HardwareDroppedFramesAvailable=false）、TimestampSource、TimestampFrequencyHz、HardwareTimestampAvailable=false、HardwareTimestamp=null、HardwareSampleAgeMs=null；Sequence 是 long 软件序号，AgeMs 为主机读取完成后的年龄，不含此前 RX 排队时间。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
