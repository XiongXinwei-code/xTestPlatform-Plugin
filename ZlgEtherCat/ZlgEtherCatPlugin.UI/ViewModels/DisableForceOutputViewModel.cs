using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class DisableForceOutputViewModel : ZlgViewModelBase<DisableForceOutputSetting>
{
    public DisableForceOutputOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<DisableForceOutputOperation, string>> OperationChoices => [new(DisableForceOutputOperation.DisableForceOutput, "解除强制输出")];
    public string OperationTitle => Operation switch
    {
        DisableForceOutputOperation.DisableForceOutput => "解除强制输出",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        DisableForceOutputOperation.DisableForceOutput => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n 本操作要求整个连接没有任何绑定轴；即使目标是其他从站，也须先停轴、去使能并解绑。\n返回：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(DisableForceOutputSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        DisableForceOutputOperation.DisableForceOutput => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        DisableForceOutputOperation.DisableForceOutput => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; NumericValueChanged(nameof(SlaveIndex)); Changed(); }
    }
    public string SlaveIndexText { get => GetNumericText(nameof(SlaveIndex), Model.SlaveIndex); set => SetIntegerText(nameof(SlaveIndex), value, parsed => SlaveIndex = parsed); }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(DisableForceOutputSetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        DisableForceOutputOperation.DisableForceOutput => "从站 ENI 索引（常量）",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        DisableForceOutputOperation.DisableForceOutput => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        _ => ""
    };

    public string SlaveIndexExpression
    {
        get => Model.SlaveIndexExpression;
        set { if (Equals(Model.SlaveIndexExpression, value)) return; Model.SlaveIndexExpression = value; Changed(); }
    }
    public bool IsSlaveIndexExpressionActive => Model.ActiveFields.Contains(nameof(DisableForceOutputSetting.SlaveIndexExpression));
    public Visibility SlaveIndexExpressionVisibility => IsSlaveIndexExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexExpressionLabel => Operation switch
    {
        DisableForceOutputOperation.DisableForceOutput => "从站 ENI 索引表达式（可选）",
        _ => ""
    };
    public string SlaveIndexExpressionInfo => Operation switch
    {
        DisableForceOutputOperation.DisableForceOutput => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(DisableForceOutputSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        DisableForceOutputOperation.DisableForceOutput => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        DisableForceOutputOperation.DisableForceOutput => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(DisableForceOutputSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        DisableForceOutputOperation.DisableForceOutput => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        DisableForceOutputOperation.DisableForceOutput => "可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
