using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class WriteBoardDoViewModel : ZlgViewModelBase<WriteBoardDoSetting>
{
    public WriteBoardDoOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<WriteBoardDoOperation, string>> OperationChoices => [new(WriteBoardDoOperation.WriteBoardDo, "写入板载 DO")];
    public string OperationTitle => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "写入板载 DO",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n 本操作要求整个连接没有任何绑定轴；即使目标是其他从站，也须先停轴、去使能并解绑。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(WriteBoardDoSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int Value
    {
        get => Model.Value;
        set { if (Equals(Model.Value, value)) return; Model.Value = value; NumericValueChanged(nameof(Value)); Changed(); }
    }
    public string ValueText { get => GetNumericText(nameof(Value), Model.Value); set => SetIntegerText(nameof(Value), value, parsed => Value = parsed); }
    public bool IsValueActive => Model.ActiveFields.Contains(nameof(WriteBoardDoSetting.Value));
    public Visibility ValueVisibility => IsValueActive ? Visibility.Visible : Visibility.Collapsed;
    public string ValueLabel => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "DO 位图",
        _ => ""
    };
    public string ValueInfo => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "兼容常量；ValueExpression 非空时使用表达式结果。板载 DO 整体位图，非 EtherCAT 从站输出；会更新整个位图。 整数支持十进制和 0x 十六进制。 范围：0～65535。",
        _ => ""
    };

    public string ValueExpression
    {
        get => Model.ValueExpression;
        set { if (Equals(Model.ValueExpression, value)) return; Model.ValueExpression = value; Changed(); }
    }
    public bool IsValueExpressionActive => Model.ActiveFields.Contains(nameof(WriteBoardDoSetting.ValueExpression));
    public Visibility ValueExpressionVisibility => IsValueExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string ValueExpressionLabel => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "DO 位图表达式（可选）",
        _ => ""
    };
    public string ValueExpressionInfo => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "留空使用上方常量；非空时用此表达式的结果作为本次输出，避免改写步骤配置。 范围：0～65535。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(WriteBoardDoSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(WriteBoardDoSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        WriteBoardDoOperation.WriteBoardDo => "可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
