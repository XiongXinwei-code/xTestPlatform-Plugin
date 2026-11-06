using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class CloseViewModel : ZlgViewModelBase<CloseSetting>
{
    public CloseOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<CloseOperation, string>> OperationChoices => [new(CloseOperation.Close, "关闭板卡通道")];
    public string OperationTitle => Operation switch
    {
        CloseOperation.Close => "关闭板卡通道",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        CloseOperation.Close => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n关闭连接并释放自己的资源；连接不存在报错；等待真实调用退出，不把软超时当作关闭完成。 正常关闭前必须停止、去使能并解绑全部轴，并显式停止本连接启动的板载 PWM/脉冲输出；通道隔离后的清理仍不证明物理停止。\n返回：对象：Closed、PhysicalStopConfirmed、Detail；Closed 只表示通信句柄关闭。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(CloseSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        CloseOperation.Close => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        CloseOperation.Close => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(CloseSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        CloseOperation.Close => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        CloseOperation.Close => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(CloseSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        CloseOperation.Close => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        CloseOperation.Close => "可选已声明变量完整路径；对象：Closed、PhysicalStopConfirmed、Detail；Closed 只表示通信句柄关闭。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
