using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class StartBoardLatchViewModel : ZlgViewModelBase<StartBoardLatchSetting>
{
    public StartBoardLatchOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<StartBoardLatchOperation, string>> OperationChoices => [new(StartBoardLatchOperation.StartBoardLatch, "启动板载锁存")];
    public string OperationTitle => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "启动板载锁存",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。\n前置步骤：配置板载编码器锁存（ZlgEtherCat.ConfigureBoardLatch）。\nContinuous 须与已应用的锁存配置相同；Count=0 触发锁存但不记录。 启动前清除旧触发状态，不清编码器计数值；需要复位计数使用独立 ResetBoardLatchCounter。 板载 PWM/脉冲/锁存共享整板归属；其他通道持有归属时拒绝操作。输出启动后需显式停止，锁存配置归属保持到通道关闭。 锁存运行中或停止结果未确认时，必须先成功执行 StopBoardLatch。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(StartBoardLatchSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public bool Continuous
    {
        get => Model.Continuous;
        set { if (Equals(Model.Continuous, value)) return; Model.Continuous = value; Changed(); }
    }
    public bool IsContinuousActive => Model.ActiveFields.Contains(nameof(StartBoardLatchSetting.Continuous));
    public Visibility ContinuousVisibility => IsContinuousActive ? Visibility.Visible : Visibility.Collapsed;
    public string ContinuousLabel => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "连续锁存",
        _ => ""
    };
    public string ContinuousInfo => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "",
        _ => ""
    };

    public int Count
    {
        get => Model.Count;
        set { if (Equals(Model.Count, value)) return; Model.Count = value; NumericValueChanged(nameof(Count)); Changed(); }
    }
    public string CountText { get => GetNumericText(nameof(Count), Model.Count); set => SetIntegerText(nameof(Count), value, parsed => Count = parsed); }
    public bool IsCountActive => Model.ActiveFields.Contains(nameof(StartBoardLatchSetting.Count));
    public Visibility CountVisibility => IsCountActive ? Visibility.Visible : Visibility.Collapsed;
    public string CountLabel => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "记录样本数",
        _ => ""
    };
    public string CountInfo => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "仅连续锁存使用的记录容量（字节数无关），0 表示触发但不记录可读样本；单次锁存仅有一个可读槽，读取 SampleIndex 应为 0 整数支持十进制和 0x 十六进制。 范围：0～1023。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(StartBoardLatchSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(StartBoardLatchSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        StartBoardLatchOperation.StartBoardLatch => "可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
