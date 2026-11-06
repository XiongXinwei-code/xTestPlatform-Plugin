using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ReadBoardPwmViewModel : ZlgViewModelBase<ReadBoardPwmSetting>
{
    public ReadBoardPwmOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ReadBoardPwmOperation, string>> OperationChoices => [new(ReadBoardPwmOperation.ReadBoardPwm, "读取板载 PWM 配置")];
    public string OperationTitle => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => "读取板载 PWM 配置",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => "目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n\n返回：对象：Frequency（Hz）、DutyPercent（%）。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ReadBoardPwmSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int Port
    {
        get => Model.Port;
        set { if (Equals(Model.Port, value)) return; Model.Port = value; NumericValueChanged(nameof(Port)); Changed(); }
    }
    public string PortText { get => GetNumericText(nameof(Port), Model.Port); set => SetIntegerText(nameof(Port), value, parsed => Port = parsed); }
    public bool IsPortActive => Model.ActiveFields.Contains(nameof(ReadBoardPwmSetting.Port));
    public Visibility PortVisibility => IsPortActive ? Visibility.Visible : Visibility.Collapsed;
    public string PortLabel => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => "输出通道",
        _ => ""
    };
    public string PortInfo => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => " 整数支持十进制和 0x 十六进制。 范围：0～3。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ReadBoardPwmSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ReadBoardPwmSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ReadBoardPwmOperation.ReadBoardPwm => "可选已声明变量完整路径；对象：Frequency（Hz）、DutyPercent（%）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
