using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class WriteDigitalOutputViewModel : ZlgViewModelBase<WriteDigitalOutputSetting>
{
    public WriteDigitalOutputOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<WriteDigitalOutputOperation, string>> OperationChoices => [new(WriteDigitalOutputOperation.WriteDigitalOutput, "写入数字量输出")];
    public string OperationTitle => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "写入数字量输出",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：使能过程映像交换（ZlgEtherCat.EnablePi）。\n 执行时必须已确认 OP；StartBus 的 RequireDc=true 时还须确认 DC 内部同步与传播延迟初始化。 任一轴绑定了目标从站时，拒绝该从站的全部原始输出写入；其他从站的输入读取不受此门禁限制。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(WriteDigitalOutputSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; NumericValueChanged(nameof(SlaveIndex)); Changed(); }
    }
    public string SlaveIndexText { get => GetNumericText(nameof(SlaveIndex), Model.SlaveIndex); set => SetIntegerText(nameof(SlaveIndex), value, parsed => SlaveIndex = parsed); }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(WriteDigitalOutputSetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "从站 ENI 索引（常量）",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        _ => ""
    };

    public string SlaveIndexExpression
    {
        get => Model.SlaveIndexExpression;
        set { if (Equals(Model.SlaveIndexExpression, value)) return; Model.SlaveIndexExpression = value; Changed(); }
    }
    public bool IsSlaveIndexExpressionActive => Model.ActiveFields.Contains(nameof(WriteDigitalOutputSetting.SlaveIndexExpression));
    public Visibility SlaveIndexExpressionVisibility => IsSlaveIndexExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexExpressionLabel => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "从站 ENI 索引表达式（可选）",
        _ => ""
    };
    public string SlaveIndexExpressionInfo => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        _ => ""
    };

    public int Offset
    {
        get => Model.Offset;
        set { if (Equals(Model.Offset, value)) return; Model.Offset = value; NumericValueChanged(nameof(Offset)); Changed(); }
    }
    public string OffsetText { get => GetNumericText(nameof(Offset), Model.Offset); set => SetIntegerText(nameof(Offset), value, parsed => Offset = parsed); }
    public bool IsOffsetActive => Model.ActiveFields.Contains(nameof(WriteDigitalOutputSetting.Offset));
    public Visibility OffsetVisibility => IsOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string OffsetLabel => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "从站输出区相对字节偏移",
        _ => ""
    };
    public string OffsetInfo => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "相对指定从站输出映像起点，从 0 开始。不是全局 Offset，也不是对象索引；实际绝对位置=ReadPiRange 中输出 Offset+此值。 整数支持十进制和 0x 十六进制。 范围：0～16777215。",
        _ => ""
    };

    public int BitOffset
    {
        get => Model.BitOffset;
        set { if (Equals(Model.BitOffset, value)) return; Model.BitOffset = value; NumericValueChanged(nameof(BitOffset)); Changed(); }
    }
    public string BitOffsetText { get => GetNumericText(nameof(BitOffset), Model.BitOffset); set => SetIntegerText(nameof(BitOffset), value, parsed => BitOffset = parsed); }
    public bool IsBitOffsetActive => Model.ActiveFields.Contains(nameof(WriteDigitalOutputSetting.BitOffset));
    public Visibility BitOffsetVisibility => IsBitOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string BitOffsetLabel => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "输出位",
        _ => ""
    };
    public string BitOffsetInfo => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "LSB0 位编号：0 为该字节最低位，7 为最高位；不等同于模块端子号，端子到位的对应关系由设备 PDO 映射确定 整数支持十进制和 0x 十六进制。 范围：0～7。",
        _ => ""
    };

    public bool Value
    {
        get => Model.Value;
        set { if (Equals(Model.Value, value)) return; Model.Value = value; Changed(); }
    }
    public bool IsValueActive => Model.ActiveFields.Contains(nameof(WriteDigitalOutputSetting.Value));
    public Visibility ValueVisibility => IsValueActive ? Visibility.Visible : Visibility.Collapsed;
    public string ValueLabel => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "输出值",
        _ => ""
    };
    public string ValueInfo => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "兼容常量；ValueExpression 非空时使用表达式结果。false 清除选定位，true 置位；不覆盖同字节其他位。",
        _ => ""
    };

    public string ValueExpression
    {
        get => Model.ValueExpression;
        set { if (Equals(Model.ValueExpression, value)) return; Model.ValueExpression = value; Changed(); }
    }
    public bool IsValueExpressionActive => Model.ActiveFields.Contains(nameof(WriteDigitalOutputSetting.ValueExpression));
    public Visibility ValueExpressionVisibility => IsValueExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string ValueExpressionLabel => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "输出值表达式（可选）",
        _ => ""
    };
    public string ValueExpressionInfo => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "留空使用上方常量；非空时用此表达式的结果作为本次输出，避免改写步骤配置。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(WriteDigitalOutputSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(WriteDigitalOutputSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        WriteDigitalOutputOperation.WriteDigitalOutput => "可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
