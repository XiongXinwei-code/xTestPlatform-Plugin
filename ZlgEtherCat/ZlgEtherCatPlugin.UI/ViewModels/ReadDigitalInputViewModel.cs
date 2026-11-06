using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ReadDigitalInputViewModel : ZlgViewModelBase<ReadDigitalInputSetting>
{
    public ReadDigitalInputOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ReadDigitalInputOperation, string>> OperationChoices => [new(ReadDigitalInputOperation.ReadDigitalInput, "读取数字量输入")];
    public string OperationTitle => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "读取数字量输入",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：启动 PDO 后台采集（ZlgEtherCat.StartAcquisition）。\n\n返回：bool：指定从站输入字节内选定位的值。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ReadDigitalInputSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; NumericValueChanged(nameof(SlaveIndex)); Changed(); }
    }
    public string SlaveIndexText { get => GetNumericText(nameof(SlaveIndex), Model.SlaveIndex); set => SetIntegerText(nameof(SlaveIndex), value, parsed => SlaveIndex = parsed); }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(ReadDigitalInputSetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "从站 ENI 索引（常量）",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        _ => ""
    };

    public string SlaveIndexExpression
    {
        get => Model.SlaveIndexExpression;
        set { if (Equals(Model.SlaveIndexExpression, value)) return; Model.SlaveIndexExpression = value; Changed(); }
    }
    public bool IsSlaveIndexExpressionActive => Model.ActiveFields.Contains(nameof(ReadDigitalInputSetting.SlaveIndexExpression));
    public Visibility SlaveIndexExpressionVisibility => IsSlaveIndexExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexExpressionLabel => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "从站 ENI 索引表达式（可选）",
        _ => ""
    };
    public string SlaveIndexExpressionInfo => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        _ => ""
    };

    public int Offset
    {
        get => Model.Offset;
        set { if (Equals(Model.Offset, value)) return; Model.Offset = value; NumericValueChanged(nameof(Offset)); Changed(); }
    }
    public string OffsetText { get => GetNumericText(nameof(Offset), Model.Offset); set => SetIntegerText(nameof(Offset), value, parsed => Offset = parsed); }
    public bool IsOffsetActive => Model.ActiveFields.Contains(nameof(ReadDigitalInputSetting.Offset));
    public Visibility OffsetVisibility => IsOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string OffsetLabel => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "从站输入区相对字节偏移",
        _ => ""
    };
    public string OffsetInfo => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "相对指定从站输入映像起点，从 0 开始。不是全局 Offset，也不是对象索引；实际绝对位置=ReadPiRange 中输入 Offset+此值。 整数支持十进制和 0x 十六进制。 范围：0～16777215。",
        _ => ""
    };

    public int BitOffset
    {
        get => Model.BitOffset;
        set { if (Equals(Model.BitOffset, value)) return; Model.BitOffset = value; NumericValueChanged(nameof(BitOffset)); Changed(); }
    }
    public string BitOffsetText { get => GetNumericText(nameof(BitOffset), Model.BitOffset); set => SetIntegerText(nameof(BitOffset), value, parsed => BitOffset = parsed); }
    public bool IsBitOffsetActive => Model.ActiveFields.Contains(nameof(ReadDigitalInputSetting.BitOffset));
    public Visibility BitOffsetVisibility => IsBitOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string BitOffsetLabel => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "输入位",
        _ => ""
    };
    public string BitOffsetInfo => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "LSB0 位编号：0 为该字节最低位，7 为最高位；不等同于模块端子号，端子到位的对应关系由设备 PDO 映射确定 整数支持十进制和 0x 十六进制。 范围：0～7。",
        _ => ""
    };

    public int MaxAgeMs
    {
        get => Model.MaxAgeMs;
        set { if (Equals(Model.MaxAgeMs, value)) return; Model.MaxAgeMs = value; NumericValueChanged(nameof(MaxAgeMs)); Changed(); }
    }
    public string MaxAgeMsText { get => GetNumericText(nameof(MaxAgeMs), Model.MaxAgeMs); set => SetIntegerText(nameof(MaxAgeMs), value, parsed => MaxAgeMs = parsed); }
    public bool IsMaxAgeMsActive => Model.ActiveFields.Contains(nameof(ReadDigitalInputSetting.MaxAgeMs));
    public Visibility MaxAgeMsVisibility => IsMaxAgeMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string MaxAgeMsLabel => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "本地接收最大年龄（ms）",
        _ => ""
    };
    public string MaxAgeMsInfo => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "从主机读取完成的单调时钟时刻起算；不含数据此前在 SDK RX 队列中的等待时间，不能据此证明设备真实采样年龄。 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ReadDigitalInputSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ReadDigitalInputSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ReadDigitalInputOperation.ReadDigitalInput => "可选已声明变量完整路径；bool：指定从站输入字节内选定位的值。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
