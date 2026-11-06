using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class PeriodicPoolStopItemViewModel : ZlgViewModelBase<PeriodicPoolStopItemSetting>
{
    public PeriodicPoolStopItemOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<PeriodicPoolStopItemOperation, string>> OperationChoices => [new(PeriodicPoolStopItemOperation.PeriodicPoolStopItem, "停止周期读取项")];
    public string OperationTitle => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "停止周期读取项",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "目标为指定连接、读取池类型及 PoolName；ItemName 只在该命名池内唯一。\n前置步骤：创建周期读取池（ZlgEtherCat.PeriodicPoolCreate）。\n等待该项在途发布退出再移除；成功返回后不再由该项写入变量；其他读取项不受影响。\n返回：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(PeriodicPoolStopItemSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string PoolName
    {
        get => Model.PoolName;
        set { if (Equals(Model.PoolName, value)) return; Model.PoolName = value; Changed(); }
    }
    public bool IsPoolNameActive => Model.ActiveFields.Contains(nameof(PeriodicPoolStopItemSetting.PoolName));
    public Visibility PoolNameVisibility => IsPoolNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string PoolNameLabel => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "功能池名",
        _ => ""
    };
    public string PoolNameInfo => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "求值结果为 string；周期池与常驻池独立命名",
        _ => ""
    };

    public string ItemName
    {
        get => Model.ItemName;
        set { if (Equals(Model.ItemName, value)) return; Model.ItemName = value; Changed(); }
    }
    public bool IsItemNameActive => Model.ActiveFields.Contains(nameof(PeriodicPoolStopItemSetting.ItemName));
    public Visibility ItemNameVisibility => IsItemNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ItemNameLabel => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "读取项名",
        _ => ""
    };
    public string ItemNameInfo => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "求值结果为 string；池内唯一",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(PeriodicPoolStopItemSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(PeriodicPoolStopItemSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        PeriodicPoolStopItemOperation.PeriodicPoolStopItem => "可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
