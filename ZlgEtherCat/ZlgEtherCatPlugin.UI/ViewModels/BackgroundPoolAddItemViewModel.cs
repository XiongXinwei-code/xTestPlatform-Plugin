using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class BackgroundPoolAddItemViewModel : ZlgViewModelBase<BackgroundPoolAddItemSetting>
{
    public BackgroundPoolAddItemOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<BackgroundPoolAddItemOperation, string>> OperationChoices => [new(BackgroundPoolAddItemOperation.BackgroundPoolAddItem, "添加常驻读取项")];
    public string OperationTitle => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "添加常驻读取项",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：创建常驻读取池（ZlgEtherCat.BackgroundPoolCreate）。\n池内 ItemName 必须唯一；只更新功能池配置，不创建线程或独立循环。 最新帧覆盖缓存可能合并中间帧；SkippedSamples 是读取项跳过的软件序号数，不是硬件丢帧统计。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(BackgroundPoolAddItemSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string PoolName
    {
        get => Model.PoolName;
        set { if (Equals(Model.PoolName, value)) return; Model.PoolName = value; Changed(); }
    }
    public bool IsPoolNameActive => Model.ActiveFields.Contains(nameof(BackgroundPoolAddItemSetting.PoolName));
    public Visibility PoolNameVisibility => IsPoolNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string PoolNameLabel => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "功能池名",
        _ => ""
    };
    public string PoolNameInfo => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "求值结果为 string；周期池与常驻池独立命名",
        _ => ""
    };

    public string ItemName
    {
        get => Model.ItemName;
        set { if (Equals(Model.ItemName, value)) return; Model.ItemName = value; Changed(); }
    }
    public bool IsItemNameActive => Model.ActiveFields.Contains(nameof(BackgroundPoolAddItemSetting.ItemName));
    public Visibility ItemNameVisibility => IsItemNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ItemNameLabel => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "读取项名",
        _ => ""
    };
    public string ItemNameInfo => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "求值结果为 string；池内唯一",
        _ => ""
    };

    public int SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; NumericValueChanged(nameof(SlaveIndex)); Changed(); }
    }
    public string SlaveIndexText { get => GetNumericText(nameof(SlaveIndex), Model.SlaveIndex); set => SetIntegerText(nameof(SlaveIndex), value, parsed => SlaveIndex = parsed); }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(BackgroundPoolAddItemSetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "从站 ENI 索引（常量）",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        _ => ""
    };

    public string SlaveIndexExpression
    {
        get => Model.SlaveIndexExpression;
        set { if (Equals(Model.SlaveIndexExpression, value)) return; Model.SlaveIndexExpression = value; Changed(); }
    }
    public bool IsSlaveIndexExpressionActive => Model.ActiveFields.Contains(nameof(BackgroundPoolAddItemSetting.SlaveIndexExpression));
    public Visibility SlaveIndexExpressionVisibility => IsSlaveIndexExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexExpressionLabel => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "从站 ENI 索引表达式（可选）",
        _ => ""
    };
    public string SlaveIndexExpressionInfo => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        _ => ""
    };

    public int Offset
    {
        get => Model.Offset;
        set { if (Equals(Model.Offset, value)) return; Model.Offset = value; NumericValueChanged(nameof(Offset)); Changed(); }
    }
    public string OffsetText { get => GetNumericText(nameof(Offset), Model.Offset); set => SetIntegerText(nameof(Offset), value, parsed => Offset = parsed); }
    public bool IsOffsetActive => Model.ActiveFields.Contains(nameof(BackgroundPoolAddItemSetting.Offset));
    public Visibility OffsetVisibility => IsOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string OffsetLabel => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "从站输入区相对字节偏移",
        _ => ""
    };
    public string OffsetInfo => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "相对指定从站输入映像起点，从 0 开始。不是全局 Offset，也不是对象索引；实际绝对位置=ReadPiRange 中输入 Offset+此值。 整数支持十进制和 0x 十六进制。 范围：0～16777215。",
        _ => ""
    };

    public int Length
    {
        get => Model.Length;
        set { if (Equals(Model.Length, value)) return; Model.Length = value; NumericValueChanged(nameof(Length)); Changed(); }
    }
    public string LengthText { get => GetNumericText(nameof(Length), Model.Length); set => SetIntegerText(nameof(Length), value, parsed => Length = parsed); }
    public bool IsLengthActive => Model.ActiveFields.Contains(nameof(BackgroundPoolAddItemSetting.Length));
    public Visibility LengthVisibility => IsLengthActive ? Visibility.Visible : Visibility.Collapsed;
    public string LengthLabel => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "字节长度",
        _ => ""
    };
    public string LengthInfo => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "Bytes 时为原始字节数；标量必须为所选类型的固定宽度（1/2/4/8 字节），范围不得超出当前从站输入区 整数支持十进制和 0x 十六进制。 范围：1～1048576。",
        _ => ""
    };

    public PdoDataType DataType
    {
        get => Model.DataType;
        set { if (Equals(Model.DataType, value)) return; Model.DataType = value; Changed(); }
    }
    public IReadOnlyList<KeyValuePair<PdoDataType, string>> DataTypeChoices => [new(PdoDataType.Bytes, "Bytes — 原始 byte[]，长度按配置，不转换"), new(PdoDataType.Boolean, "Boolean — bool，1 字节；任一位非零为 true，不是单个位"), new(PdoDataType.UInt8, "UInt8 — byte，无符号 8 位，1 字节"), new(PdoDataType.Int8, "Int8 — sbyte，有符号 8 位，1 字节"), new(PdoDataType.UInt16, "UInt16 — ushort，无符号 16 位，2 字节"), new(PdoDataType.Int16, "Int16 — short，有符号 16 位，2 字节"), new(PdoDataType.UInt32, "UInt32 — uint，无符号 32 位，4 字节"), new(PdoDataType.Int32, "Int32 — int，有符号 32 位，4 字节"), new(PdoDataType.UInt64, "UInt64 — ulong，无符号 64 位，8 字节"), new(PdoDataType.Int64, "Int64 — long，有符号 64 位，8 字节"), new(PdoDataType.Float32, "Float32 — float，IEEE 754，4 字节"), new(PdoDataType.Float64, "Float64 — double，IEEE 754，8 字节")];
    public bool IsDataTypeActive => Model.ActiveFields.Contains(nameof(BackgroundPoolAddItemSetting.DataType));
    public Visibility DataTypeVisibility => IsDataTypeActive ? Visibility.Visible : Visibility.Collapsed;
    public string DataTypeLabel => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "数据类型",
        _ => ""
    };
    public string DataTypeInfo => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "可选值：Bytes — 原始 byte[]，长度按配置，不转换；Boolean — bool，1 字节；任一位非零为 true，不是单个位；UInt8 — byte，无符号 8 位，1 字节；Int8 — sbyte，有符号 8 位，1 字节；UInt16 — ushort，无符号 16 位，2 字节；Int16 — short，有符号 16 位，2 字节；UInt32 — uint，无符号 32 位，4 字节；Int32 — int，有符号 32 位，4 字节；UInt64 — ulong，无符号 64 位，8 字节；Int64 — long，有符号 64 位，8 字节；Float32 — float，IEEE 754，4 字节；Float64 — double，IEEE 754，8 字节。多字节固定小端；没有自动缩放。单个位请用位读取/数字输入步骤。 ENI 提供字段元数据时会核对标量类型、宽度与边界；Bytes 是原始访问，不核对对象类型。非字节对齐字段须用位步骤，不能选普通标量。",
        _ => ""
    };

    public string DataVariable
    {
        get => Model.DataVariable;
        set { if (Equals(Model.DataVariable, value)) return; Model.DataVariable = value; Changed(); }
    }
    public bool IsDataVariableActive => Model.ActiveFields.Contains(nameof(BackgroundPoolAddItemSetting.DataVariable));
    public Visibility DataVariableVisibility => IsDataVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string DataVariableLabel => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "读取项结果变量",
        _ => ""
    };
    public string DataVariableInfo => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "必填的已声明变量完整路径，如 Locals.Module2Input；写入按 DataType 解码后的值（随 DataType 返回 byte[]、bool、byte/sbyte、ushort/short、uint/int、ulong/long 或 float/double；多字节固定小端），不是样本包。只写该变量，不创建派生路径。",
        _ => ""
    };

    public int MaxAgeMs
    {
        get => Model.MaxAgeMs;
        set { if (Equals(Model.MaxAgeMs, value)) return; Model.MaxAgeMs = value; NumericValueChanged(nameof(MaxAgeMs)); Changed(); }
    }
    public string MaxAgeMsText { get => GetNumericText(nameof(MaxAgeMs), Model.MaxAgeMs); set => SetIntegerText(nameof(MaxAgeMs), value, parsed => MaxAgeMs = parsed); }
    public bool IsMaxAgeMsActive => Model.ActiveFields.Contains(nameof(BackgroundPoolAddItemSetting.MaxAgeMs));
    public Visibility MaxAgeMsVisibility => IsMaxAgeMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string MaxAgeMsLabel => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "本地接收最大年龄（ms）",
        _ => ""
    };
    public string MaxAgeMsInfo => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "从主机读取完成的单调时钟时刻起算；不含数据此前在 SDK RX 队列中的等待时间，不能据此证明设备真实采样年龄。 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(BackgroundPoolAddItemSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(BackgroundPoolAddItemSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        BackgroundPoolAddItemOperation.BackgroundPoolAddItem => "可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
