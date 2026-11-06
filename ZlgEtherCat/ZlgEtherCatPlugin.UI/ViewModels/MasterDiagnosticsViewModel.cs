using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class MasterDiagnosticsViewModel : ZlgViewModelBase<MasterDiagnosticsSetting>
{
    public MasterDiagnosticsOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<MasterDiagnosticsOperation, string>> OperationChoices => [new(MasterDiagnosticsOperation.ReadMasterStatus, "读取主站状态"), new(MasterDiagnosticsOperation.ReadDiagnostics, "读取综合诊断"), new(MasterDiagnosticsOperation.ReadLinkDiagnostics, "读取链路诊断"), new(MasterDiagnosticsOperation.ReadLastError, "读取 SDK 错误详情")];
    public string OperationTitle => Operation switch
    {
        MasterDiagnosticsOperation.ReadMasterStatus => "读取主站状态",
        MasterDiagnosticsOperation.ReadDiagnostics => "读取综合诊断",
        MasterDiagnosticsOperation.ReadLinkDiagnostics => "读取链路诊断",
        MasterDiagnosticsOperation.ReadLastError => "读取 SDK 错误详情",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        MasterDiagnosticsOperation.ReadMasterStatus => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n\n返回：对象：Board、BusStatus、Running、State、RequireDc、ActiveBoardOutputs、BoardLatchOwned、BoardLatchStopUnconfirmed；State 是 EtherCAT 状态码，板载输出状态不证明电机物理停稳。",
        MasterDiagnosticsOperation.ReadDiagnostics => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n\n返回：对象：Raw（原始主从站诊断）、Updated、DcInternalSyncEstablished、DcExternalSyncEstablished、DcPropagationDelayInitialized。",
        MasterDiagnosticsOperation.ReadLinkDiagnostics => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n\n返回：对象：FirstLinkDown、Faults（链路故障及拓扑）；拓扑 PhysicalPosition 不是 SlaveIndex。",
        MasterDiagnosticsOperation.ReadLastError => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n\n返回：string：SDK 错误文本；不是自动重试或恢复操作。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(MasterDiagnosticsSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        MasterDiagnosticsOperation.ReadMasterStatus => "连接名",
        MasterDiagnosticsOperation.ReadDiagnostics => "连接名",
        MasterDiagnosticsOperation.ReadLinkDiagnostics => "连接名",
        MasterDiagnosticsOperation.ReadLastError => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        MasterDiagnosticsOperation.ReadMasterStatus => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        MasterDiagnosticsOperation.ReadDiagnostics => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        MasterDiagnosticsOperation.ReadLinkDiagnostics => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        MasterDiagnosticsOperation.ReadLastError => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(MasterDiagnosticsSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        MasterDiagnosticsOperation.ReadMasterStatus => "步骤总超时（ms）",
        MasterDiagnosticsOperation.ReadDiagnostics => "步骤总超时（ms）",
        MasterDiagnosticsOperation.ReadLinkDiagnostics => "步骤总超时（ms）",
        MasterDiagnosticsOperation.ReadLastError => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        MasterDiagnosticsOperation.ReadMasterStatus => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        MasterDiagnosticsOperation.ReadDiagnostics => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        MasterDiagnosticsOperation.ReadLinkDiagnostics => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        MasterDiagnosticsOperation.ReadLastError => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(MasterDiagnosticsSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        MasterDiagnosticsOperation.ReadMasterStatus => "结果变量",
        MasterDiagnosticsOperation.ReadDiagnostics => "结果变量",
        MasterDiagnosticsOperation.ReadLinkDiagnostics => "结果变量",
        MasterDiagnosticsOperation.ReadLastError => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        MasterDiagnosticsOperation.ReadMasterStatus => "可选已声明变量完整路径；对象：Board、BusStatus、Running、State、RequireDc、ActiveBoardOutputs、BoardLatchOwned、BoardLatchStopUnconfirmed；State 是 EtherCAT 状态码，板载输出状态不证明电机物理停稳。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        MasterDiagnosticsOperation.ReadDiagnostics => "可选已声明变量完整路径；对象：Raw（原始主从站诊断）、Updated、DcInternalSyncEstablished、DcExternalSyncEstablished、DcPropagationDelayInitialized。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        MasterDiagnosticsOperation.ReadLinkDiagnostics => "可选已声明变量完整路径；对象：FirstLinkDown、Faults（链路故障及拓扑）；拓扑 PhysicalPosition 不是 SlaveIndex。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        MasterDiagnosticsOperation.ReadLastError => "可选已声明变量完整路径；string：SDK 错误文本；不是自动重试或恢复操作。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
