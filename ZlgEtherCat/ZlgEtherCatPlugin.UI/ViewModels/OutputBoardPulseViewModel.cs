using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class OutputBoardPulseViewModel : ZlgViewModelBase<OutputBoardPulseSetting>
{
    public OutputBoardPulseOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<OutputBoardPulseOperation, string>> OperationChoices => [new(OutputBoardPulseOperation.OutputBoardPulse, "输出板载脉冲")];
    public string OperationTitle => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => "输出板载脉冲",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => "目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。\n前置步骤：配置板载脉冲（ZlgEtherCat.ConfigureBoardPulse）。\n 板载 PWM/脉冲/锁存共享整板归属；其他通道持有归属时拒绝操作。输出启动后需显式停止，锁存配置归属保持到通道关闭。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(OutputBoardPulseSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int Port
    {
        get => Model.Port;
        set { if (Equals(Model.Port, value)) return; Model.Port = value; NumericValueChanged(nameof(Port)); Changed(); }
    }
    public string PortText { get => GetNumericText(nameof(Port), Model.Port); set => SetIntegerText(nameof(Port), value, parsed => Port = parsed); }
    public bool IsPortActive => Model.ActiveFields.Contains(nameof(OutputBoardPulseSetting.Port));
    public Visibility PortVisibility => IsPortActive ? Visibility.Visible : Visibility.Collapsed;
    public string PortLabel => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => "输出通道",
        _ => ""
    };
    public string PortInfo => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => " 整数支持十进制和 0x 十六进制。 范围：0～3。",
        _ => ""
    };

    public int PulseCount
    {
        get => Model.PulseCount;
        set { if (Equals(Model.PulseCount, value)) return; Model.PulseCount = value; NumericValueChanged(nameof(PulseCount)); Changed(); }
    }
    public string PulseCountText { get => GetNumericText(nameof(PulseCount), Model.PulseCount); set => SetIntegerText(nameof(PulseCount), value, parsed => PulseCount = parsed); }
    public bool IsPulseCountActive => Model.ActiveFields.Contains(nameof(OutputBoardPulseSetting.PulseCount));
    public Visibility PulseCountVisibility => IsPulseCountActive ? Visibility.Visible : Visibility.Collapsed;
    public string PulseCountLabel => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => "脉冲数量",
        _ => ""
    };
    public string PulseCountInfo => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => " 整数支持十进制和 0x 十六进制。 范围：1～2147483647。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(OutputBoardPulseSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(OutputBoardPulseSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        OutputBoardPulseOperation.OutputBoardPulse => "可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
