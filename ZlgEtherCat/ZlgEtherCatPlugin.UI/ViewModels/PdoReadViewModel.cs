using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class PdoReadViewModel : ZlgViewModelBase<PdoReadSetting>
{
    public PdoReadOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<PdoReadOperation, string>> OperationChoices => [new(PdoReadOperation.ReadPdo, "读取 PDO 当前值"), new(PdoReadOperation.ReadPdoBits, "按位读取 PDO")];
    public string OperationTitle => Operation switch
    {
        PdoReadOperation.ReadPdo => "读取 PDO 当前值",
        PdoReadOperation.ReadPdoBits => "按位读取 PDO",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        PdoReadOperation.ReadPdo => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：启动 PDO 后台采集（ZlgEtherCat.StartAcquisition）。\n读取完整快照，不直接竞争 SDK 输入队列。\n返回：随 DataType 返回 byte[]、bool、byte/sbyte、ushort/short、uint/int、ulong/long 或 float/double；多字节固定小端；ReturnSample=true 时返回 Value、Sequence、SlaveIndex、Offset、Length、DataType、ReceivedTimestamp、LocalAgeMs，以及 TimestampSource=\"HostMonotonicReadCompletion\"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；ReceivedTimestamp 是主机读取完成的单调时钟刻度，不是设备采样时刻，LocalAgeMs 不包含此前 RX 排队时间。",
        PdoReadOperation.ReadPdoBits => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：启动 PDO 后台采集（ZlgEtherCat.StartAcquisition）。\n\n返回：ulong：按 LSB0 从低位起打包的无符号位域；BitLength=1 仍是 ulong（0/1）。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(PdoReadSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        PdoReadOperation.ReadPdo => "连接名",
        PdoReadOperation.ReadPdoBits => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        PdoReadOperation.ReadPdo => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        PdoReadOperation.ReadPdoBits => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; NumericValueChanged(nameof(SlaveIndex)); Changed(); }
    }
    public string SlaveIndexText { get => GetNumericText(nameof(SlaveIndex), Model.SlaveIndex); set => SetIntegerText(nameof(SlaveIndex), value, parsed => SlaveIndex = parsed); }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(PdoReadSetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        PdoReadOperation.ReadPdo => "从站 ENI 索引（常量）",
        PdoReadOperation.ReadPdoBits => "从站 ENI 索引（常量）",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        PdoReadOperation.ReadPdo => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        PdoReadOperation.ReadPdoBits => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        _ => ""
    };

    public string SlaveIndexExpression
    {
        get => Model.SlaveIndexExpression;
        set { if (Equals(Model.SlaveIndexExpression, value)) return; Model.SlaveIndexExpression = value; Changed(); }
    }
    public bool IsSlaveIndexExpressionActive => Model.ActiveFields.Contains(nameof(PdoReadSetting.SlaveIndexExpression));
    public Visibility SlaveIndexExpressionVisibility => IsSlaveIndexExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexExpressionLabel => Operation switch
    {
        PdoReadOperation.ReadPdo => "从站 ENI 索引表达式（可选）",
        PdoReadOperation.ReadPdoBits => "从站 ENI 索引表达式（可选）",
        _ => ""
    };
    public string SlaveIndexExpressionInfo => Operation switch
    {
        PdoReadOperation.ReadPdo => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        PdoReadOperation.ReadPdoBits => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        _ => ""
    };

    public int Offset
    {
        get => Model.Offset;
        set { if (Equals(Model.Offset, value)) return; Model.Offset = value; NumericValueChanged(nameof(Offset)); Changed(); }
    }
    public string OffsetText { get => GetNumericText(nameof(Offset), Model.Offset); set => SetIntegerText(nameof(Offset), value, parsed => Offset = parsed); }
    public bool IsOffsetActive => Model.ActiveFields.Contains(nameof(PdoReadSetting.Offset));
    public Visibility OffsetVisibility => IsOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string OffsetLabel => Operation switch
    {
        PdoReadOperation.ReadPdo => "从站输入区相对字节偏移",
        PdoReadOperation.ReadPdoBits => "从站输入区相对字节偏移",
        _ => ""
    };
    public string OffsetInfo => Operation switch
    {
        PdoReadOperation.ReadPdo => "相对指定从站输入映像起点，从 0 开始。不是全局 Offset，也不是对象索引；实际绝对位置=ReadPiRange 中输入 Offset+此值。 整数支持十进制和 0x 十六进制。 范围：0～16777215。",
        PdoReadOperation.ReadPdoBits => "相对指定从站输入映像起点，从 0 开始。不是全局 Offset，也不是对象索引；实际绝对位置=ReadPiRange 中输入 Offset+此值。 整数支持十进制和 0x 十六进制。 范围：0～16777215。",
        _ => ""
    };

    public int Length
    {
        get => Model.Length;
        set { if (Equals(Model.Length, value)) return; Model.Length = value; NumericValueChanged(nameof(Length)); Changed(); }
    }
    public string LengthText { get => GetNumericText(nameof(Length), Model.Length); set => SetIntegerText(nameof(Length), value, parsed => Length = parsed); }
    public bool IsLengthActive => Model.ActiveFields.Contains(nameof(PdoReadSetting.Length));
    public Visibility LengthVisibility => IsLengthActive ? Visibility.Visible : Visibility.Collapsed;
    public string LengthLabel => Operation switch
    {
        PdoReadOperation.ReadPdo => "字节长度",
        _ => ""
    };
    public string LengthInfo => Operation switch
    {
        PdoReadOperation.ReadPdo => "Bytes 时为原始字节数；标量必须为所选类型的固定宽度（1/2/4/8 字节），范围不得超出当前从站输入区 整数支持十进制和 0x 十六进制。 范围：1～1048576。",
        _ => ""
    };

    public PdoDataType DataType
    {
        get => Model.DataType;
        set { if (Equals(Model.DataType, value)) return; Model.DataType = value; Changed(); }
    }
    public IReadOnlyList<KeyValuePair<PdoDataType, string>> DataTypeChoices => [new(PdoDataType.Bytes, "Bytes — 原始 byte[]，长度按配置，不转换"), new(PdoDataType.Boolean, "Boolean — bool，1 字节；任一位非零为 true，不是单个位"), new(PdoDataType.UInt8, "UInt8 — byte，无符号 8 位，1 字节"), new(PdoDataType.Int8, "Int8 — sbyte，有符号 8 位，1 字节"), new(PdoDataType.UInt16, "UInt16 — ushort，无符号 16 位，2 字节"), new(PdoDataType.Int16, "Int16 — short，有符号 16 位，2 字节"), new(PdoDataType.UInt32, "UInt32 — uint，无符号 32 位，4 字节"), new(PdoDataType.Int32, "Int32 — int，有符号 32 位，4 字节"), new(PdoDataType.UInt64, "UInt64 — ulong，无符号 64 位，8 字节"), new(PdoDataType.Int64, "Int64 — long，有符号 64 位，8 字节"), new(PdoDataType.Float32, "Float32 — float，IEEE 754，4 字节"), new(PdoDataType.Float64, "Float64 — double，IEEE 754，8 字节")];
    public bool IsDataTypeActive => Model.ActiveFields.Contains(nameof(PdoReadSetting.DataType));
    public Visibility DataTypeVisibility => IsDataTypeActive ? Visibility.Visible : Visibility.Collapsed;
    public string DataTypeLabel => Operation switch
    {
        PdoReadOperation.ReadPdo => "数据类型",
        _ => ""
    };
    public string DataTypeInfo => Operation switch
    {
        PdoReadOperation.ReadPdo => "可选值：Bytes — 原始 byte[]，长度按配置，不转换；Boolean — bool，1 字节；任一位非零为 true，不是单个位；UInt8 — byte，无符号 8 位，1 字节；Int8 — sbyte，有符号 8 位，1 字节；UInt16 — ushort，无符号 16 位，2 字节；Int16 — short，有符号 16 位，2 字节；UInt32 — uint，无符号 32 位，4 字节；Int32 — int，有符号 32 位，4 字节；UInt64 — ulong，无符号 64 位，8 字节；Int64 — long，有符号 64 位，8 字节；Float32 — float，IEEE 754，4 字节；Float64 — double，IEEE 754，8 字节。多字节固定小端；没有自动缩放。单个位请用位读取/数字输入步骤。 ENI 提供字段元数据时会核对标量类型、宽度与边界；Bytes 是原始访问，不核对对象类型。非字节对齐字段须用位步骤，不能选普通标量。",
        _ => ""
    };

    public int MaxAgeMs
    {
        get => Model.MaxAgeMs;
        set { if (Equals(Model.MaxAgeMs, value)) return; Model.MaxAgeMs = value; NumericValueChanged(nameof(MaxAgeMs)); Changed(); }
    }
    public string MaxAgeMsText { get => GetNumericText(nameof(MaxAgeMs), Model.MaxAgeMs); set => SetIntegerText(nameof(MaxAgeMs), value, parsed => MaxAgeMs = parsed); }
    public bool IsMaxAgeMsActive => Model.ActiveFields.Contains(nameof(PdoReadSetting.MaxAgeMs));
    public Visibility MaxAgeMsVisibility => IsMaxAgeMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string MaxAgeMsLabel => Operation switch
    {
        PdoReadOperation.ReadPdo => "本地接收最大年龄（ms）",
        PdoReadOperation.ReadPdoBits => "本地接收最大年龄（ms）",
        _ => ""
    };
    public string MaxAgeMsInfo => Operation switch
    {
        PdoReadOperation.ReadPdo => "从主机读取完成的单调时钟时刻起算；不含数据此前在 SDK RX 队列中的等待时间，不能据此证明设备真实采样年龄。 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        PdoReadOperation.ReadPdoBits => "从主机读取完成的单调时钟时刻起算；不含数据此前在 SDK RX 队列中的等待时间，不能据此证明设备真实采样年龄。 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public bool ReturnSample
    {
        get => Model.ReturnSample;
        set { if (Equals(Model.ReturnSample, value)) return; Model.ReturnSample = value; Changed(); }
    }
    public bool IsReturnSampleActive => Model.ActiveFields.Contains(nameof(PdoReadSetting.ReturnSample));
    public Visibility ReturnSampleVisibility => IsReturnSampleActive ? Visibility.Visible : Visibility.Collapsed;
    public string ReturnSampleLabel => Operation switch
    {
        PdoReadOperation.ReadPdo => "返回带序号的样本",
        _ => ""
    };
    public string ReturnSampleInfo => Operation switch
    {
        PdoReadOperation.ReadPdo => "false 保持旧行为仅返回解码值；true 返回 Value 与 Sequence 等样本元数据，结果变量应能接收对象，可将其 Sequence 用于下一次 AfterSequenceExpression",
        _ => ""
    };

    public int BitOffset
    {
        get => Model.BitOffset;
        set { if (Equals(Model.BitOffset, value)) return; Model.BitOffset = value; NumericValueChanged(nameof(BitOffset)); Changed(); }
    }
    public string BitOffsetText { get => GetNumericText(nameof(BitOffset), Model.BitOffset); set => SetIntegerText(nameof(BitOffset), value, parsed => BitOffset = parsed); }
    public bool IsBitOffsetActive => Model.ActiveFields.Contains(nameof(PdoReadSetting.BitOffset));
    public Visibility BitOffsetVisibility => IsBitOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string BitOffsetLabel => Operation switch
    {
        PdoReadOperation.ReadPdoBits => "字节内起始位",
        _ => ""
    };
    public string BitOffsetInfo => Operation switch
    {
        PdoReadOperation.ReadPdoBits => "LSB0 位编号：0 为该字节最低位，7 为最高位；不等同于模块端子号，端子到位的对应关系由设备 PDO 映射确定 整数支持十进制和 0x 十六进制。 范围：0～7。",
        _ => ""
    };

    public int BitLength
    {
        get => Model.BitLength;
        set { if (Equals(Model.BitLength, value)) return; Model.BitLength = value; NumericValueChanged(nameof(BitLength)); Changed(); }
    }
    public string BitLengthText { get => GetNumericText(nameof(BitLength), Model.BitLength); set => SetIntegerText(nameof(BitLength), value, parsed => BitLength = parsed); }
    public bool IsBitLengthActive => Model.ActiveFields.Contains(nameof(PdoReadSetting.BitLength));
    public Visibility BitLengthVisibility => IsBitLengthActive ? Visibility.Visible : Visibility.Collapsed;
    public string BitLengthLabel => Operation switch
    {
        PdoReadOperation.ReadPdoBits => "位数",
        _ => ""
    };
    public string BitLengthInfo => Operation switch
    {
        PdoReadOperation.ReadPdoBits => "可跨字节；从 BitOffset 起读取 1～64 位并返回 ulong。符号扩展和工程单位换算由后续逻辑显式处理 整数支持十进制和 0x 十六进制。 范围：1～64。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(PdoReadSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        PdoReadOperation.ReadPdo => "步骤总超时（ms）",
        PdoReadOperation.ReadPdoBits => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        PdoReadOperation.ReadPdo => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        PdoReadOperation.ReadPdoBits => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(PdoReadSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        PdoReadOperation.ReadPdo => "结果变量",
        PdoReadOperation.ReadPdoBits => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        PdoReadOperation.ReadPdo => "可选已声明变量完整路径；随 DataType 返回 byte[]、bool、byte/sbyte、ushort/short、uint/int、ulong/long 或 float/double；多字节固定小端；ReturnSample=true 时返回 Value、Sequence、SlaveIndex、Offset、Length、DataType、ReceivedTimestamp、LocalAgeMs，以及 TimestampSource=\"HostMonotonicReadCompletion\"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；ReceivedTimestamp 是主机读取完成的单调时钟刻度，不是设备采样时刻，LocalAgeMs 不包含此前 RX 排队时间。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        PdoReadOperation.ReadPdoBits => "可选已声明变量完整路径；ulong：按 LSB0 从低位起打包的无符号位域；BitLength=1 仍是 ulong（0/1）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
