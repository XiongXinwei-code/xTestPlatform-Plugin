using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class BeginExitViewModel : ZlgViewModelBase<BeginExitSetting>
{
    public BeginExitOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<BeginExitOperation, string>> OperationChoices => [new(BeginExitOperation.BeginExit, "开始退出流程")];
    public string OperationTitle => Operation switch
    {
        BeginExitOperation.BeginExit => "开始退出流程",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        BeginExitOperation.BeginExit => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n锁存指定通道的退出状态，停止接受新的业务写入和运动请求；保留现有输入读取以及停止、去使能、解绑和关闭步骤，用于完成退出。该步骤不新建工作线程，只锁存退出，不证明运动已经停止。\n返回：对象：ExitRequested、PhysicalStopConfirmed；不表示驱动已停止。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(BeginExitSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        BeginExitOperation.BeginExit => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        BeginExitOperation.BeginExit => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(BeginExitSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        BeginExitOperation.BeginExit => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        BeginExitOperation.BeginExit => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(BeginExitSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        BeginExitOperation.BeginExit => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        BeginExitOperation.BeginExit => "可选已声明变量完整路径；对象：ExitRequested、PhysicalStopConfirmed；不表示驱动已停止。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
