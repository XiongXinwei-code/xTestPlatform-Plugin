using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ReadSdoViewModel : ZlgViewModelBase<ReadSdoSetting>
{
    public ReadSdoOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ReadSdoOperation, string>> OperationChoices => [new(ReadSdoOperation.ReadSdo, "读取 SDO 参数")];
    public string OperationTitle => Operation switch
    {
        ReadSdoOperation.ReadSdo => "读取 SDO 参数",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ReadSdoOperation.ReadSdo => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：加载 ENI 并启动主站（ZlgEtherCat.StartBus）。\n Queue 周期输出运行期间拒绝本操作，避免长邮箱/维护调用阻塞唯一 SDK 线程；需先完成停轴、去使能、解绑并停止周期输出。\n返回：随 DataType 返回 byte[]、bool、byte/sbyte、ushort/short、uint/int、ulong/long 或 float/double；多字节固定小端；Bytes 时返回实际收到的完整字节块。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ReadSdoSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ReadSdoOperation.ReadSdo => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ReadSdoOperation.ReadSdo => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; NumericValueChanged(nameof(SlaveIndex)); Changed(); }
    }
    public string SlaveIndexText { get => GetNumericText(nameof(SlaveIndex), Model.SlaveIndex); set => SetIntegerText(nameof(SlaveIndex), value, parsed => SlaveIndex = parsed); }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(ReadSdoSetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        ReadSdoOperation.ReadSdo => "从站 ENI 索引（常量）",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        ReadSdoOperation.ReadSdo => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        _ => ""
    };

    public string SlaveIndexExpression
    {
        get => Model.SlaveIndexExpression;
        set { if (Equals(Model.SlaveIndexExpression, value)) return; Model.SlaveIndexExpression = value; Changed(); }
    }
    public bool IsSlaveIndexExpressionActive => Model.ActiveFields.Contains(nameof(ReadSdoSetting.SlaveIndexExpression));
    public Visibility SlaveIndexExpressionVisibility => IsSlaveIndexExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexExpressionLabel => Operation switch
    {
        ReadSdoOperation.ReadSdo => "从站 ENI 索引表达式（可选）",
        _ => ""
    };
    public string SlaveIndexExpressionInfo => Operation switch
    {
        ReadSdoOperation.ReadSdo => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        _ => ""
    };

    public int Index
    {
        get => Model.Index;
        set { if (Equals(Model.Index, value)) return; Model.Index = value; NumericValueChanged(nameof(Index)); Changed(); }
    }
    public string IndexText { get => GetNumericText(nameof(Index), Model.Index); set => SetIntegerText(nameof(Index), value, parsed => Index = parsed); }
    public bool IsIndexActive => Model.ActiveFields.Contains(nameof(ReadSdoSetting.Index));
    public Visibility IndexVisibility => IsIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string IndexLabel => Operation switch
    {
        ReadSdoOperation.ReadSdo => "CoE 对象索引（常量）",
        _ => ""
    };
    public string IndexInfo => Operation switch
    {
        ReadSdoOperation.ReadSdo => "当前从站对象字典索引，范围 0～65535；支持 0x6040 或十进制 24640。不能填 PDO 字节偏移或从站编号。 整数支持十进制和 0x 十六进制。 范围：0～65535。",
        _ => ""
    };

    public int SubIndex
    {
        get => Model.SubIndex;
        set { if (Equals(Model.SubIndex, value)) return; Model.SubIndex = value; NumericValueChanged(nameof(SubIndex)); Changed(); }
    }
    public string SubIndexText { get => GetNumericText(nameof(SubIndex), Model.SubIndex); set => SetIntegerText(nameof(SubIndex), value, parsed => SubIndex = parsed); }
    public bool IsSubIndexActive => Model.ActiveFields.Contains(nameof(ReadSdoSetting.SubIndex));
    public Visibility SubIndexVisibility => IsSubIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SubIndexLabel => Operation switch
    {
        ReadSdoOperation.ReadSdo => "子索引",
        _ => ""
    };
    public string SubIndexInfo => Operation switch
    {
        ReadSdoOperation.ReadSdo => "同一对象内的子索引，0～255；不是从站索引。CompleteAccess=true 时仅允许 0 或 1，具体起点由设备对象字典确定 整数支持十进制和 0x 十六进制。 范围：0～255。",
        _ => ""
    };

    public bool CompleteAccess
    {
        get => Model.CompleteAccess;
        set { if (Equals(Model.CompleteAccess, value)) return; Model.CompleteAccess = value; Changed(); }
    }
    public bool IsCompleteAccessActive => Model.ActiveFields.Contains(nameof(ReadSdoSetting.CompleteAccess));
    public Visibility CompleteAccessVisibility => IsCompleteAccessActive ? Visibility.Visible : Visibility.Collapsed;
    public string CompleteAccessLabel => Operation switch
    {
        ReadSdoOperation.ReadSdo => "完全访问",
        _ => ""
    };
    public string CompleteAccessInfo => Operation switch
    {
        ReadSdoOperation.ReadSdo => "false 只访问一个 Index:SubIndex；true 是同一对象内多个子索引的整体访问，不是读取所有从站，当前仅支持 DataType=Bytes；需设备支持",
        _ => ""
    };

    public int Capacity
    {
        get => Model.Capacity;
        set { if (Equals(Model.Capacity, value)) return; Model.Capacity = value; NumericValueChanged(nameof(Capacity)); Changed(); }
    }
    public string CapacityText { get => GetNumericText(nameof(Capacity), Model.Capacity); set => SetIntegerText(nameof(Capacity), value, parsed => Capacity = parsed); }
    public bool IsCapacityActive => Model.ActiveFields.Contains(nameof(ReadSdoSetting.Capacity));
    public Visibility CapacityVisibility => IsCapacityActive ? Visibility.Visible : Visibility.Collapsed;
    public string CapacityLabel => Operation switch
    {
        ReadSdoOperation.ReadSdo => "接收缓冲容量",
        _ => ""
    };
    public string CapacityInfo => Operation switch
    {
        ReadSdoOperation.ReadSdo => "最大接收字节容量，可以大于标量宽度；标量模式容量不得小于类型宽度，并要求实际返回长度与类型一致；CompleteAccess 应选 Bytes 并提供完整块容量 整数支持十进制和 0x 十六进制。 范围：1～1048576。",
        _ => ""
    };

    public PdoDataType DataType
    {
        get => Model.DataType;
        set { if (Equals(Model.DataType, value)) return; Model.DataType = value; Changed(); }
    }
    public IReadOnlyList<KeyValuePair<PdoDataType, string>> DataTypeChoices => [new(PdoDataType.Bytes, "Bytes — 原始 byte[]，长度按配置，不转换"), new(PdoDataType.Boolean, "Boolean — bool，1 字节；任一位非零为 true，不是单个位"), new(PdoDataType.UInt8, "UInt8 — byte，无符号 8 位，1 字节"), new(PdoDataType.Int8, "Int8 — sbyte，有符号 8 位，1 字节"), new(PdoDataType.UInt16, "UInt16 — ushort，无符号 16 位，2 字节"), new(PdoDataType.Int16, "Int16 — short，有符号 16 位，2 字节"), new(PdoDataType.UInt32, "UInt32 — uint，无符号 32 位，4 字节"), new(PdoDataType.Int32, "Int32 — int，有符号 32 位，4 字节"), new(PdoDataType.UInt64, "UInt64 — ulong，无符号 64 位，8 字节"), new(PdoDataType.Int64, "Int64 — long，有符号 64 位，8 字节"), new(PdoDataType.Float32, "Float32 — float，IEEE 754，4 字节"), new(PdoDataType.Float64, "Float64 — double，IEEE 754，8 字节")];
    public bool IsDataTypeActive => Model.ActiveFields.Contains(nameof(ReadSdoSetting.DataType));
    public Visibility DataTypeVisibility => IsDataTypeActive ? Visibility.Visible : Visibility.Collapsed;
    public string DataTypeLabel => Operation switch
    {
        ReadSdoOperation.ReadSdo => "数据类型",
        _ => ""
    };
    public string DataTypeInfo => Operation switch
    {
        ReadSdoOperation.ReadSdo => "可选值：Bytes — 原始 byte[]，长度按配置，不转换；Boolean — bool，1 字节；任一位非零为 true，不是单个位；UInt8 — byte，无符号 8 位，1 字节；Int8 — sbyte，有符号 8 位，1 字节；UInt16 — ushort，无符号 16 位，2 字节；Int16 — short，有符号 16 位，2 字节；UInt32 — uint，无符号 32 位，4 字节；Int32 — int，有符号 32 位，4 字节；UInt64 — ulong，无符号 64 位，8 字节；Int64 — long，有符号 64 位，8 字节；Float32 — float，IEEE 754，4 字节；Float64 — double，IEEE 754，8 字节。多字节固定小端；没有自动缩放。单个位请用位读取/数字输入步骤。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ReadSdoSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ReadSdoOperation.ReadSdo => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ReadSdoOperation.ReadSdo => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ReadSdoSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ReadSdoOperation.ReadSdo => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ReadSdoOperation.ReadSdo => "可选已声明变量完整路径；随 DataType 返回 byte[]、bool、byte/sbyte、ushort/short、uint/int、ulong/long 或 float/double；多字节固定小端；Bytes 时返回实际收到的完整字节块。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
