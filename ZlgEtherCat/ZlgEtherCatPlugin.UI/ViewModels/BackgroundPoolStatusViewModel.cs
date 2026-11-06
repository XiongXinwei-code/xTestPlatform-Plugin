using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class BackgroundPoolStatusViewModel : ZlgViewModelBase<BackgroundPoolStatusSetting>
{
    public BackgroundPoolStatusOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<BackgroundPoolStatusOperation, string>> OperationChoices => [new(BackgroundPoolStatusOperation.BackgroundPoolStatus, "读取常驻读取池状态")];
    public string OperationTitle => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => "读取常驻读取池状态",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => "目标为指定连接、读取池类型及 PoolName；ItemName 只在该命名池内唯一。\n前置步骤：创建常驻读取池（ZlgEtherCat.BackgroundPoolCreate）。\n 最新帧覆盖缓存可能合并中间帧；SkippedSamples 是读取项跳过的软件序号数，不是硬件丢帧统计。 RxQueue 为最后一次观测值，须同时查看 ObservationAgeMs 与 MaxPollGapMs；SDK 阻塞时监控也会延迟。LastObservationTimestamp/LastRecoveryTimestamp 为主机队列观测/恢复时刻的单调时钟刻度；HighWatermarkEvents/BacklogWarningEvents 计告警事件段，FullQueueObservations 计满深度观测次数。高水位/持续积压状态可随观测变化，OverflowSuspected 为疑似事件锁存，HardwareDroppedFrames=null。Queue 容量未知时仍记录深度/持续积压，但禁用高水位和满判定；非 Queue 模式禁用队列告警。告警由既有后台工作循环发送，不在 SDK 工作线程中调用宿主日志；重复消息受 RxAlarmRepeatMs 节流并可合并，NotificationError 显示通知失败。\n返回：对象：WorkerStarts、InputAgeMs、AcquisitionRunning、Isolated、Priority（Requested 为 AcquisitionPriority?，Actual/RestoreTarget 为可空的 .NET 线程优先级名称，Error 为可空的设置或恢复错误）、RxQueue（Available、Monitoring、Capacity、CurrentDepth、PeakDepth、HighWatermarkPercent、HighWatermarkDepth、BacklogWarningMs、AlarmRepeatMs、LatestOnly、HighWatermarkActive、BacklogWarningActive、OverflowSuspected、HighWatermarkEvents、BacklogWarningEvents、FullQueueObservations、LastObservationTimestamp、ObservationAgeMs、MaxPollGapMs、LastRecoveryTimestamp、LastAlarm、NotificationError、HardwareDroppedFrames=null、HardwareDroppedFramesAvailable=false）、TimestampSource、TimestampFrequencyHz、HardwareTimestampAvailable=false、HardwareTimestamp=null、Items；每项含 Name、Enabled、Publications、LastSequence、Error、SkippedSamples；InputAgeMs 不含此前 RX 排队时间。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(BackgroundPoolStatusSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string PoolName
    {
        get => Model.PoolName;
        set { if (Equals(Model.PoolName, value)) return; Model.PoolName = value; Changed(); }
    }
    public bool IsPoolNameActive => Model.ActiveFields.Contains(nameof(BackgroundPoolStatusSetting.PoolName));
    public Visibility PoolNameVisibility => IsPoolNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string PoolNameLabel => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => "功能池名",
        _ => ""
    };
    public string PoolNameInfo => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => "求值结果为 string；周期池与常驻池独立命名",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(BackgroundPoolStatusSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(BackgroundPoolStatusSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        BackgroundPoolStatusOperation.BackgroundPoolStatus => "可选已声明变量完整路径；对象：WorkerStarts、InputAgeMs、AcquisitionRunning、Isolated、Priority（Requested 为 AcquisitionPriority?，Actual/RestoreTarget 为可空的 .NET 线程优先级名称，Error 为可空的设置或恢复错误）、RxQueue（Available、Monitoring、Capacity、CurrentDepth、PeakDepth、HighWatermarkPercent、HighWatermarkDepth、BacklogWarningMs、AlarmRepeatMs、LatestOnly、HighWatermarkActive、BacklogWarningActive、OverflowSuspected、HighWatermarkEvents、BacklogWarningEvents、FullQueueObservations、LastObservationTimestamp、ObservationAgeMs、MaxPollGapMs、LastRecoveryTimestamp、LastAlarm、NotificationError、HardwareDroppedFrames=null、HardwareDroppedFramesAvailable=false）、TimestampSource、TimestampFrequencyHz、HardwareTimestampAvailable=false、HardwareTimestamp=null、Items；每项含 Name、Enabled、Publications、LastSequence、Error、SkippedSamples；InputAgeMs 不含此前 RX 排队时间。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
