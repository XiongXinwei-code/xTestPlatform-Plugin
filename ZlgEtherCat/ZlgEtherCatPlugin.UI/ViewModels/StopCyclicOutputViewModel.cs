using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class StopCyclicOutputViewModel : ZlgViewModelBase<StopCyclicOutputSetting>
{
    public StopCyclicOutputOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<StopCyclicOutputOperation, string>> OperationChoices => [new(StopCyclicOutputOperation.StopCyclicOutput, "停止 PDO 周期输出")];
    public string OperationTitle => Operation switch
    {
        StopCyclicOutputOperation.StopCyclicOutput => "停止 PDO 周期输出",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        StopCyclicOutputOperation.StopCyclicOutput => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：启动 PDO 周期输出（ZlgEtherCat.StartCyclicOutput）。\n停止成功表示周期输出工作已退出；存在受控轴时须先完成停轴。 本操作要求所有轴已停止、去使能并解绑；只执行 StopMotion 而保留绑定仍不能执行本步骤。\n返回：对象：Stopped、QueuedFramesWithdrawn；停止续发不撤回 SDK 队列中已有帧。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(StopCyclicOutputSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        StopCyclicOutputOperation.StopCyclicOutput => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        StopCyclicOutputOperation.StopCyclicOutput => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(StopCyclicOutputSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        StopCyclicOutputOperation.StopCyclicOutput => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        StopCyclicOutputOperation.StopCyclicOutput => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(StopCyclicOutputSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        StopCyclicOutputOperation.StopCyclicOutput => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        StopCyclicOutputOperation.StopCyclicOutput => "可选已声明变量完整路径；对象：Stopped、QueuedFramesWithdrawn；停止续发不撤回 SDK 队列中已有帧。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
