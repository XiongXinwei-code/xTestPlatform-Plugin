using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class StartAcquisitionViewModel : ZlgViewModelBase<StartAcquisitionSetting>
{
    public StartAcquisitionOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<StartAcquisitionOperation, string>> OperationChoices => [new(StartAcquisitionOperation.StartAcquisition, "启动 PDO 后台采集")];
    public string OperationTitle => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "启动 PDO 后台采集",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：使能过程映像交换（ZlgEtherCat.EnablePi）。\n每通道唯一固定采集工作循环；步骤返回后继续采集，重复启动报错；不为读取项创建线程。 要求 ENI 包含非空输入映像；当前主站必须保持 OP，RequireDc=true 时 DC 同步必须有效。优先级应用于本通道共享 SDK 线程，包含 SDO/输出访问，应用后读取实际值确认；不新增 RX 线程。采集配置改变须先停止再启动，成功启动才重置 RX 诊断。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(StartAcquisitionSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int NativeWaitMs
    {
        get => Model.NativeWaitMs;
        set { if (Equals(Model.NativeWaitMs, value)) return; Model.NativeWaitMs = value; NumericValueChanged(nameof(NativeWaitMs)); Changed(); }
    }
    public string NativeWaitMsText { get => GetNumericText(nameof(NativeWaitMs), Model.NativeWaitMs); set => SetIntegerText(nameof(NativeWaitMs), value, parsed => NativeWaitMs = parsed); }
    public bool IsNativeWaitMsActive => Model.ActiveFields.Contains(nameof(StartAcquisitionSetting.NativeWaitMs));
    public Visibility NativeWaitMsVisibility => IsNativeWaitMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string NativeWaitMsLabel => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "单次队列等待（ms）",
        _ => ""
    };
    public string NativeWaitMsInfo => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => " 整数支持十进制和 0x 十六进制。 范围：1～1000。",
        _ => ""
    };

    public int HistoryCapacity
    {
        get => Model.HistoryCapacity;
        set { if (Equals(Model.HistoryCapacity, value)) return; Model.HistoryCapacity = value; NumericValueChanged(nameof(HistoryCapacity)); Changed(); }
    }
    public string HistoryCapacityText { get => GetNumericText(nameof(HistoryCapacity), Model.HistoryCapacity); set => SetIntegerText(nameof(HistoryCapacity), value, parsed => HistoryCapacity = parsed); }
    public bool IsHistoryCapacityActive => Model.ActiveFields.Contains(nameof(StartAcquisitionSetting.HistoryCapacity));
    public Visibility HistoryCapacityVisibility => IsHistoryCapacityActive ? Visibility.Visible : Visibility.Collapsed;
    public string HistoryCapacityLabel => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "历史缓冲容量",
        _ => ""
    };
    public string HistoryCapacityInfo => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => " 整数支持十进制和 0x 十六进制。 范围：1～1000000。",
        _ => ""
    };

    public bool LatestOnly
    {
        get => Model.LatestOnly;
        set { if (Equals(Model.LatestOnly, value)) return; Model.LatestOnly = value; Changed(); }
    }
    public bool IsLatestOnlyActive => Model.ActiveFields.Contains(nameof(StartAcquisitionSetting.LatestOnly));
    public Visibility LatestOnlyVisibility => IsLatestOnlyActive ? Visibility.Visible : Visibility.Collapsed;
    public string LatestOnlyLabel => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "跳过积压，仅取最新",
        _ => ""
    };
    public string LatestOnlyInfo => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "主动清理旧队列数据以优先获得较新输入；这是用户选择的软件丢弃策略，不等于硬件丢帧。监控在清旧前记录最后观测，不能证明所有总线帧均被读取。",
        _ => ""
    };

    public AcquisitionPriority AcquisitionThreadPriority
    {
        get => Model.AcquisitionThreadPriority;
        set { if (Equals(Model.AcquisitionThreadPriority, value)) return; Model.AcquisitionThreadPriority = value; Changed(); }
    }
    public IReadOnlyList<KeyValuePair<AcquisitionPriority, string>> AcquisitionThreadPriorityChoices => [new(AcquisitionPriority.Normal, "Normal — 普通，默认值 0"), new(AcquisitionPriority.AboveNormal, "AboveNormal — 高于普通，值 1"), new(AcquisitionPriority.Highest, "Highest — 最高，值 2；仍由 Windows 调度，不保证硬实时")];
    public bool IsAcquisitionThreadPriorityActive => Model.ActiveFields.Contains(nameof(StartAcquisitionSetting.AcquisitionThreadPriority));
    public Visibility AcquisitionThreadPriorityVisibility => IsAcquisitionThreadPriorityActive ? Visibility.Visible : Visibility.Collapsed;
    public string AcquisitionThreadPriorityLabel => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "共享 SDK 线程优先级",
        _ => ""
    };
    public string AcquisitionThreadPriorityInfo => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "可选值：Normal — 普通，默认值 0；AboveNormal — 高于普通，值 1；Highest — 最高，值 2；仍由 Windows 调度，不保证硬实时 作用于本通道整个共享 SDK 线程，包括输入采集、SDO 和输出，不新增 RX 线程。StartAcquisition 应用并读回确认；StopAcquisition 恢复原优先级。更改须先停止再启动；优先级不能保证 Windows 实时调度。",
        _ => ""
    };

    public int RxHighWatermarkPercent
    {
        get => Model.RxHighWatermarkPercent;
        set { if (Equals(Model.RxHighWatermarkPercent, value)) return; Model.RxHighWatermarkPercent = value; NumericValueChanged(nameof(RxHighWatermarkPercent)); Changed(); }
    }
    public string RxHighWatermarkPercentText { get => GetNumericText(nameof(RxHighWatermarkPercent), Model.RxHighWatermarkPercent); set => SetIntegerText(nameof(RxHighWatermarkPercent), value, parsed => RxHighWatermarkPercent = parsed); }
    public bool IsRxHighWatermarkPercentActive => Model.ActiveFields.Contains(nameof(StartAcquisitionSetting.RxHighWatermarkPercent));
    public Visibility RxHighWatermarkPercentVisibility => IsRxHighWatermarkPercentActive ? Visibility.Visible : Visibility.Collapsed;
    public string RxHighWatermarkPercentLabel => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "RX 队列高水位（%）",
        _ => ""
    };
    public string RxHighWatermarkPercentInfo => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "Queue 模式且 RX 容量已知时，按容量百分比计算高水位；容量未知时不进行高水位/满判定。只描述 SDK 最后观测的深度，队列满仅提示溢出疑似，不报告硬件丢帧数。 整数支持十进制和 0x 十六进制。 范围：1～100。",
        _ => ""
    };

    public int RxBacklogWarningMs
    {
        get => Model.RxBacklogWarningMs;
        set { if (Equals(Model.RxBacklogWarningMs, value)) return; Model.RxBacklogWarningMs = value; NumericValueChanged(nameof(RxBacklogWarningMs)); Changed(); }
    }
    public string RxBacklogWarningMsText { get => GetNumericText(nameof(RxBacklogWarningMs), Model.RxBacklogWarningMs); set => SetIntegerText(nameof(RxBacklogWarningMs), value, parsed => RxBacklogWarningMs = parsed); }
    public bool IsRxBacklogWarningMsActive => Model.ActiveFields.Contains(nameof(StartAcquisitionSetting.RxBacklogWarningMs));
    public Visibility RxBacklogWarningMsVisibility => IsRxBacklogWarningMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string RxBacklogWarningMsLabel => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "RX 积压告警持续时间（ms）",
        _ => ""
    };
    public string RxBacklogWarningMsInfo => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "Queue 模式下 RX 深度持续非零的积压告警时间阈值，以主机观测为依据；容量未知时仍监测持续积压。SDK 调用阻塞时观测及告警会延迟，不是独立实时监控线程。 整数支持十进制和 0x 十六进制。 范围：1～60000。",
        _ => ""
    };

    public int RxAlarmRepeatMs
    {
        get => Model.RxAlarmRepeatMs;
        set { if (Equals(Model.RxAlarmRepeatMs, value)) return; Model.RxAlarmRepeatMs = value; NumericValueChanged(nameof(RxAlarmRepeatMs)); Changed(); }
    }
    public string RxAlarmRepeatMsText { get => GetNumericText(nameof(RxAlarmRepeatMs), Model.RxAlarmRepeatMs); set => SetIntegerText(nameof(RxAlarmRepeatMs), value, parsed => RxAlarmRepeatMs = parsed); }
    public bool IsRxAlarmRepeatMsActive => Model.ActiveFields.Contains(nameof(StartAcquisitionSetting.RxAlarmRepeatMs));
    public Visibility RxAlarmRepeatMsVisibility => IsRxAlarmRepeatMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string RxAlarmRepeatMsLabel => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "RX 重复告警最小间隔（ms）",
        _ => ""
    };
    public string RxAlarmRepeatMsInfo => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "本通道相邻 RX 通知（含恢复）的最小间隔；使用已有后台工作循环通知，不为告警或读取项新增线程。 整数支持十进制和 0x 十六进制。 范围：100～3600000。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(StartAcquisitionSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(StartAcquisitionSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        StartAcquisitionOperation.StartAcquisition => "可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
