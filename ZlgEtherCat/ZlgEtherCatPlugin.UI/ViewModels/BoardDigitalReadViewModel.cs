using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class BoardDigitalReadViewModel : ZlgViewModelBase<BoardDigitalReadSetting>
{
    public BoardDigitalReadOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<BoardDigitalReadOperation, string>> OperationChoices => [new(BoardDigitalReadOperation.ReadBoardDi, "读取板载 DI"), new(BoardDigitalReadOperation.ReadBoardDo, "读取板载 DO")];
    public string OperationTitle => Operation switch
    {
        BoardDigitalReadOperation.ReadBoardDi => "读取板载 DI",
        BoardDigitalReadOperation.ReadBoardDo => "读取板载 DO",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        BoardDigitalReadOperation.ReadBoardDi => "目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n\n返回：uint：板载 DI 位图，位对应板载端口而非 EtherCAT 从站模块。",
        BoardDigitalReadOperation.ReadBoardDo => "目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n\n返回：uint：板载 DO 位图，位对应板载端口而非 EtherCAT 从站模块。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(BoardDigitalReadSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        BoardDigitalReadOperation.ReadBoardDi => "连接名",
        BoardDigitalReadOperation.ReadBoardDo => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        BoardDigitalReadOperation.ReadBoardDi => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        BoardDigitalReadOperation.ReadBoardDo => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(BoardDigitalReadSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        BoardDigitalReadOperation.ReadBoardDi => "步骤总超时（ms）",
        BoardDigitalReadOperation.ReadBoardDo => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        BoardDigitalReadOperation.ReadBoardDi => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        BoardDigitalReadOperation.ReadBoardDo => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(BoardDigitalReadSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        BoardDigitalReadOperation.ReadBoardDi => "结果变量",
        BoardDigitalReadOperation.ReadBoardDo => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        BoardDigitalReadOperation.ReadBoardDi => "可选已声明变量完整路径；uint：板载 DI 位图，位对应板载端口而非 EtherCAT 从站模块。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        BoardDigitalReadOperation.ReadBoardDo => "可选已声明变量完整路径；uint：板载 DO 位图，位对应板载端口而非 EtherCAT 从站模块。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
