using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class RequestStateViewModel : ZlgViewModelBase<RequestStateSetting>
{
    public RequestStateOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<RequestStateOperation, string>> OperationChoices => [new(RequestStateOperation.RequestState, "切换并等待主站状态")];
    public string OperationTitle => Operation switch
    {
        RequestStateOperation.RequestState => "切换并等待主站状态",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        RequestStateOperation.RequestState => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：加载 ENI 并启动主站（ZlgEtherCat.StartBus）。\n请求状态后轮询确认；未达到目标在总期限内报错。 请求非 OP 状态前必须停止、去使能并解绑所有轴；需要保持周期读取时不要在采集中切换离开 OP。 BeginExit 后只允许请求非 OP 状态；不能重新请求 OP。\n返回：byte：已确认的 EtherCAT 状态码（INIT=1、PREOP=2、BOOT=3、SAFEOP=4、OP=8）。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(RequestStateSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        RequestStateOperation.RequestState => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        RequestStateOperation.RequestState => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public MasterState State
    {
        get => Model.State;
        set { if (Equals(Model.State, value)) return; Model.State = value; Changed(); }
    }
    public IReadOnlyList<KeyValuePair<MasterState, string>> StateChoices => [new(MasterState.Init, "Init — INIT 初始化"), new(MasterState.PreOperational, "PreOperational — PREOP 预运行／邮箱配置"), new(MasterState.Bootstrap, "Bootstrap — BOOT 引导／设备支持的维护"), new(MasterState.SafeOperational, "SafeOperational — SAFEOP 安全运行协议状态"), new(MasterState.Operational, "Operational — OP 运行状态")];
    public bool IsStateActive => Model.ActiveFields.Contains(nameof(RequestStateSetting.State));
    public Visibility StateVisibility => IsStateActive ? Visibility.Visible : Visibility.Collapsed;
    public string StateLabel => Operation switch
    {
        RequestStateOperation.RequestState => "目标状态",
        _ => ""
    };
    public string StateInfo => Operation switch
    {
        RequestStateOperation.RequestState => "可选值：Init — INIT 初始化；PreOperational — PREOP 预运行／邮箱配置；Bootstrap — BOOT 引导／设备支持的维护；SafeOperational — SAFEOP 安全运行协议状态；Operational — OP 运行状态",
        _ => ""
    };

    public int PollIntervalMs
    {
        get => Model.PollIntervalMs;
        set { if (Equals(Model.PollIntervalMs, value)) return; Model.PollIntervalMs = value; NumericValueChanged(nameof(PollIntervalMs)); Changed(); }
    }
    public string PollIntervalMsText { get => GetNumericText(nameof(PollIntervalMs), Model.PollIntervalMs); set => SetIntegerText(nameof(PollIntervalMs), value, parsed => PollIntervalMs = parsed); }
    public bool IsPollIntervalMsActive => Model.ActiveFields.Contains(nameof(RequestStateSetting.PollIntervalMs));
    public Visibility PollIntervalMsVisibility => IsPollIntervalMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string PollIntervalMsLabel => Operation switch
    {
        RequestStateOperation.RequestState => "状态检查间隔（ms）",
        _ => ""
    };
    public string PollIntervalMsInfo => Operation switch
    {
        RequestStateOperation.RequestState => " 整数支持十进制和 0x 十六进制。 范围：1～10000。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(RequestStateSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        RequestStateOperation.RequestState => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        RequestStateOperation.RequestState => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(RequestStateSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        RequestStateOperation.RequestState => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        RequestStateOperation.RequestState => "可选已声明变量完整路径；byte：已确认的 EtherCAT 状态码（INIT=1、PREOP=2、BOOT=3、SAFEOP=4、OP=8）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
