using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class WaitNextPdoViewModel : ZlgViewModelBase<WaitNextPdoSetting>
{
    public WaitNextPdoOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<WaitNextPdoOperation, string>> OperationChoices => [new(WaitNextPdoOperation.WaitNextPdo, "等待新 PDO 样本")];
    public string OperationTitle => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "等待新 PDO 样本",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：启动 PDO 后台采集（ZlgEtherCat.StartAcquisition）。\n等待序号大于 AfterSequence 的样本；主机序号不代表硬件周期号。 仅 Queue 模式具有新输入帧证据；循环使用应启用 ReturnSample 并把返回 Sequence 传入下一次 AfterSequenceExpression。\n返回：随 DataType 返回 byte[]、bool、byte/sbyte、ushort/short、uint/int、ulong/long 或 float/double；多字节固定小端；ReturnSample=true 时返回 Value、Sequence、SlaveIndex、Offset、Length、DataType、ReceivedTimestamp、LocalAgeMs，以及 TimestampSource=\"HostMonotonicReadCompletion\"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；ReceivedTimestamp 是主机读取完成的单调时钟刻度，不是设备采样时刻，LocalAgeMs 不包含此前 RX 排队时间；后续可引用 Sequence。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(WaitNextPdoSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; NumericValueChanged(nameof(SlaveIndex)); Changed(); }
    }
    public string SlaveIndexText { get => GetNumericText(nameof(SlaveIndex), Model.SlaveIndex); set => SetIntegerText(nameof(SlaveIndex), value, parsed => SlaveIndex = parsed); }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(WaitNextPdoSetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "从站 ENI 索引（常量）",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        _ => ""
    };

    public string SlaveIndexExpression
    {
        get => Model.SlaveIndexExpression;
        set { if (Equals(Model.SlaveIndexExpression, value)) return; Model.SlaveIndexExpression = value; Changed(); }
    }
    public bool IsSlaveIndexExpressionActive => Model.ActiveFields.Contains(nameof(WaitNextPdoSetting.SlaveIndexExpression));
    public Visibility SlaveIndexExpressionVisibility => IsSlaveIndexExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexExpressionLabel => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "从站 ENI 索引表达式（可选）",
        _ => ""
    };
    public string SlaveIndexExpressionInfo => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        _ => ""
    };

    public int Offset
    {
        get => Model.Offset;
        set { if (Equals(Model.Offset, value)) return; Model.Offset = value; NumericValueChanged(nameof(Offset)); Changed(); }
    }
    public string OffsetText { get => GetNumericText(nameof(Offset), Model.Offset); set => SetIntegerText(nameof(Offset), value, parsed => Offset = parsed); }
    public bool IsOffsetActive => Model.ActiveFields.Contains(nameof(WaitNextPdoSetting.Offset));
    public Visibility OffsetVisibility => IsOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string OffsetLabel => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "从站输入区相对字节偏移",
        _ => ""
    };
    public string OffsetInfo => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "相对指定从站输入映像起点，从 0 开始。不是全局 Offset，也不是对象索引；实际绝对位置=ReadPiRange 中输入 Offset+此值。 整数支持十进制和 0x 十六进制。 范围：0～16777215。",
        _ => ""
    };

    public int Length
    {
        get => Model.Length;
        set { if (Equals(Model.Length, value)) return; Model.Length = value; NumericValueChanged(nameof(Length)); Changed(); }
    }
    public string LengthText { get => GetNumericText(nameof(Length), Model.Length); set => SetIntegerText(nameof(Length), value, parsed => Length = parsed); }
    public bool IsLengthActive => Model.ActiveFields.Contains(nameof(WaitNextPdoSetting.Length));
    public Visibility LengthVisibility => IsLengthActive ? Visibility.Visible : Visibility.Collapsed;
    public string LengthLabel => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "字节长度",
        _ => ""
    };
    public string LengthInfo => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "Bytes 时为原始字节数；标量必须为所选类型的固定宽度（1/2/4/8 字节），范围不得超出当前从站输入区 整数支持十进制和 0x 十六进制。 范围：1～1048576。",
        _ => ""
    };

    public PdoDataType DataType
    {
        get => Model.DataType;
        set { if (Equals(Model.DataType, value)) return; Model.DataType = value; Changed(); }
    }
    public IReadOnlyList<KeyValuePair<PdoDataType, string>> DataTypeChoices => [new(PdoDataType.Bytes, "Bytes — 原始 byte[]，长度按配置，不转换"), new(PdoDataType.Boolean, "Boolean — bool，1 字节；任一位非零为 true，不是单个位"), new(PdoDataType.UInt8, "UInt8 — byte，无符号 8 位，1 字节"), new(PdoDataType.Int8, "Int8 — sbyte，有符号 8 位，1 字节"), new(PdoDataType.UInt16, "UInt16 — ushort，无符号 16 位，2 字节"), new(PdoDataType.Int16, "Int16 — short，有符号 16 位，2 字节"), new(PdoDataType.UInt32, "UInt32 — uint，无符号 32 位，4 字节"), new(PdoDataType.Int32, "Int32 — int，有符号 32 位，4 字节"), new(PdoDataType.UInt64, "UInt64 — ulong，无符号 64 位，8 字节"), new(PdoDataType.Int64, "Int64 — long，有符号 64 位，8 字节"), new(PdoDataType.Float32, "Float32 — float，IEEE 754，4 字节"), new(PdoDataType.Float64, "Float64 — double，IEEE 754，8 字节")];
    public bool IsDataTypeActive => Model.ActiveFields.Contains(nameof(WaitNextPdoSetting.DataType));
    public Visibility DataTypeVisibility => IsDataTypeActive ? Visibility.Visible : Visibility.Collapsed;
    public string DataTypeLabel => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "数据类型",
        _ => ""
    };
    public string DataTypeInfo => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "可选值：Bytes — 原始 byte[]，长度按配置，不转换；Boolean — bool，1 字节；任一位非零为 true，不是单个位；UInt8 — byte，无符号 8 位，1 字节；Int8 — sbyte，有符号 8 位，1 字节；UInt16 — ushort，无符号 16 位，2 字节；Int16 — short，有符号 16 位，2 字节；UInt32 — uint，无符号 32 位，4 字节；Int32 — int，有符号 32 位，4 字节；UInt64 — ulong，无符号 64 位，8 字节；Int64 — long，有符号 64 位，8 字节；Float32 — float，IEEE 754，4 字节；Float64 — double，IEEE 754，8 字节。多字节固定小端；没有自动缩放。单个位请用位读取/数字输入步骤。 ENI 提供字段元数据时会核对标量类型、宽度与边界；Bytes 是原始访问，不核对对象类型。非字节对齐字段须用位步骤，不能选普通标量。",
        _ => ""
    };

    public int MaxAgeMs
    {
        get => Model.MaxAgeMs;
        set { if (Equals(Model.MaxAgeMs, value)) return; Model.MaxAgeMs = value; NumericValueChanged(nameof(MaxAgeMs)); Changed(); }
    }
    public string MaxAgeMsText { get => GetNumericText(nameof(MaxAgeMs), Model.MaxAgeMs); set => SetIntegerText(nameof(MaxAgeMs), value, parsed => MaxAgeMs = parsed); }
    public bool IsMaxAgeMsActive => Model.ActiveFields.Contains(nameof(WaitNextPdoSetting.MaxAgeMs));
    public Visibility MaxAgeMsVisibility => IsMaxAgeMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string MaxAgeMsLabel => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "本地接收最大年龄（ms）",
        _ => ""
    };
    public string MaxAgeMsInfo => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "从主机读取完成的单调时钟时刻起算；不含数据此前在 SDK RX 队列中的等待时间，不能据此证明设备真实采样年龄。 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public int AfterSequence
    {
        get => Model.AfterSequence;
        set { if (Equals(Model.AfterSequence, value)) return; Model.AfterSequence = value; NumericValueChanged(nameof(AfterSequence)); Changed(); }
    }
    public string AfterSequenceText { get => GetNumericText(nameof(AfterSequence), Model.AfterSequence); set => SetIntegerText(nameof(AfterSequence), value, parsed => AfterSequence = parsed); }
    public bool IsAfterSequenceActive => Model.ActiveFields.Contains(nameof(WaitNextPdoSetting.AfterSequence));
    public Visibility AfterSequenceVisibility => IsAfterSequenceActive ? Visibility.Visible : Visibility.Collapsed;
    public string AfterSequenceLabel => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "已读取的软件样本序号",
        _ => ""
    };
    public string AfterSequenceInfo => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "long 软件样本序号的兼容常量输入（此字段保留 int 范围）；仅返回序号大于它的样本，AfterSequenceExpression 非空时优先；不使用设备周期号 整数支持十进制和 0x 十六进制。 范围：0～2147483647。",
        _ => ""
    };

    public string AfterSequenceExpression
    {
        get => Model.AfterSequenceExpression;
        set { if (Equals(Model.AfterSequenceExpression, value)) return; Model.AfterSequenceExpression = value; Changed(); }
    }
    public bool IsAfterSequenceExpressionActive => Model.ActiveFields.Contains(nameof(WaitNextPdoSetting.AfterSequenceExpression));
    public Visibility AfterSequenceExpressionVisibility => IsAfterSequenceExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string AfterSequenceExpressionLabel => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "已读取的软件样本序号表达式（可选）",
        _ => ""
    };
    public string AfterSequenceExpressionInfo => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "留空使用上方常量；非空时读取 long 软件样本序号，例如上一张样本的 Sequence，结果必须大于等于 0。",
        _ => ""
    };

    public bool ReturnSample
    {
        get => Model.ReturnSample;
        set { if (Equals(Model.ReturnSample, value)) return; Model.ReturnSample = value; Changed(); }
    }
    public bool IsReturnSampleActive => Model.ActiveFields.Contains(nameof(WaitNextPdoSetting.ReturnSample));
    public Visibility ReturnSampleVisibility => IsReturnSampleActive ? Visibility.Visible : Visibility.Collapsed;
    public string ReturnSampleLabel => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "返回带序号的样本",
        _ => ""
    };
    public string ReturnSampleInfo => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "false 保持旧行为仅返回解码值；true 返回 Value 与 Sequence 等样本元数据，结果变量应能接收对象，可将其 Sequence 用于下一次 AfterSequenceExpression",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(WaitNextPdoSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(WaitNextPdoSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        WaitNextPdoOperation.WaitNextPdo => "可选已声明变量完整路径；随 DataType 返回 byte[]、bool、byte/sbyte、ushort/short、uint/int、ulong/long 或 float/double；多字节固定小端；ReturnSample=true 时返回 Value、Sequence、SlaveIndex、Offset、Length、DataType、ReceivedTimestamp、LocalAgeMs，以及 TimestampSource=\"HostMonotonicReadCompletion\"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；ReceivedTimestamp 是主机读取完成的单调时钟刻度，不是设备采样时刻，LocalAgeMs 不包含此前 RX 排队时间；后续可引用 Sequence。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
