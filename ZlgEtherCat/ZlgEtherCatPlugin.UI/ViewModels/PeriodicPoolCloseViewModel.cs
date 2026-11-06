using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class PeriodicPoolCloseViewModel : ZlgViewModelBase<PeriodicPoolCloseSetting>
{
    public PeriodicPoolCloseOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<PeriodicPoolCloseOperation, string>> OperationChoices => [new(PeriodicPoolCloseOperation.PeriodicPoolClose, "关闭周期读取池")];
    public string OperationTitle => Operation switch
    {
        PeriodicPoolCloseOperation.PeriodicPoolClose => "关闭周期读取池",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        PeriodicPoolCloseOperation.PeriodicPoolClose => "目标为指定连接、读取池类型及 PoolName；ItemName 只在该命名池内唯一。\n前置步骤：创建周期读取池（ZlgEtherCat.PeriodicPoolCreate）。\n等待池内在途发布结束再移除；池不存在报错，不关闭通道采集。\n返回：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(PeriodicPoolCloseSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        PeriodicPoolCloseOperation.PeriodicPoolClose => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        PeriodicPoolCloseOperation.PeriodicPoolClose => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string PoolName
    {
        get => Model.PoolName;
        set { if (Equals(Model.PoolName, value)) return; Model.PoolName = value; Changed(); }
    }
    public bool IsPoolNameActive => Model.ActiveFields.Contains(nameof(PeriodicPoolCloseSetting.PoolName));
    public Visibility PoolNameVisibility => IsPoolNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string PoolNameLabel => Operation switch
    {
        PeriodicPoolCloseOperation.PeriodicPoolClose => "功能池名",
        _ => ""
    };
    public string PoolNameInfo => Operation switch
    {
        PeriodicPoolCloseOperation.PeriodicPoolClose => "求值结果为 string；周期池与常驻池独立命名",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(PeriodicPoolCloseSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        PeriodicPoolCloseOperation.PeriodicPoolClose => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        PeriodicPoolCloseOperation.PeriodicPoolClose => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(PeriodicPoolCloseSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        PeriodicPoolCloseOperation.PeriodicPoolClose => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        PeriodicPoolCloseOperation.PeriodicPoolClose => "可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
