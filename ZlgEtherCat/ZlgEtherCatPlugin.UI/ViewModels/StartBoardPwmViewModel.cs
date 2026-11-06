using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class StartBoardPwmViewModel : ZlgViewModelBase<StartBoardPwmSetting>
{
    public StartBoardPwmOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<StartBoardPwmOperation, string>> OperationChoices => [new(StartBoardPwmOperation.StartBoardPwm, "启动板载 PWM")];
    public string OperationTitle => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => "启动板载 PWM",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => "目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n 板载 PWM/脉冲/锁存共享整板归属；其他通道持有归属时拒绝操作。输出启动后需显式停止，锁存配置归属保持到通道关闭。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(StartBoardPwmSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int Port
    {
        get => Model.Port;
        set { if (Equals(Model.Port, value)) return; Model.Port = value; NumericValueChanged(nameof(Port)); Changed(); }
    }
    public string PortText { get => GetNumericText(nameof(Port), Model.Port); set => SetIntegerText(nameof(Port), value, parsed => Port = parsed); }
    public bool IsPortActive => Model.ActiveFields.Contains(nameof(StartBoardPwmSetting.Port));
    public Visibility PortVisibility => IsPortActive ? Visibility.Visible : Visibility.Collapsed;
    public string PortLabel => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => "输出通道",
        _ => ""
    };
    public string PortInfo => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => " 整数支持十进制和 0x 十六进制。 范围：0～3。",
        _ => ""
    };

    public int FrequencyHz
    {
        get => Model.FrequencyHz;
        set { if (Equals(Model.FrequencyHz, value)) return; Model.FrequencyHz = value; NumericValueChanged(nameof(FrequencyHz)); Changed(); }
    }
    public string FrequencyHzText { get => GetNumericText(nameof(FrequencyHz), Model.FrequencyHz); set => SetIntegerText(nameof(FrequencyHz), value, parsed => FrequencyHz = parsed); }
    public bool IsFrequencyHzActive => Model.ActiveFields.Contains(nameof(StartBoardPwmSetting.FrequencyHz));
    public Visibility FrequencyHzVisibility => IsFrequencyHzActive ? Visibility.Visible : Visibility.Collapsed;
    public string FrequencyHzLabel => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => "频率（Hz）",
        _ => ""
    };
    public string FrequencyHzInfo => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => " 整数支持十进制和 0x 十六进制。 范围：1～10000000。",
        _ => ""
    };

    public int Duty
    {
        get => Model.Duty;
        set { if (Equals(Model.Duty, value)) return; Model.Duty = value; NumericValueChanged(nameof(Duty)); Changed(); }
    }
    public string DutyText { get => GetNumericText(nameof(Duty), Model.Duty); set => SetIntegerText(nameof(Duty), value, parsed => Duty = parsed); }
    public bool IsDutyActive => Model.ActiveFields.Contains(nameof(StartBoardPwmSetting.Duty));
    public Visibility DutyVisibility => IsDutyActive ? Visibility.Visible : Visibility.Collapsed;
    public string DutyLabel => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => "占空比",
        _ => ""
    };
    public string DutyInfo => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => " 整数支持十进制和 0x 十六进制。 范围：0～100。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(StartBoardPwmSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(StartBoardPwmSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        StartBoardPwmOperation.StartBoardPwm => "可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
