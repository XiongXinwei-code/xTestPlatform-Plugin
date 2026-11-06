using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class PdoWriteViewModel : ZlgViewModelBase<PdoWriteSetting>
{
    public PdoWriteOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<PdoWriteOperation, string>> OperationChoices => [new(PdoWriteOperation.WritePdo, "写入 PDO"), new(PdoWriteOperation.WritePdoMasked, "掩码写入 PDO")];
    public string OperationTitle => Operation switch
    {
        PdoWriteOperation.WritePdo => "写入 PDO",
        PdoWriteOperation.WritePdoMasked => "掩码写入 PDO",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        PdoWriteOperation.WritePdo => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：使能过程映像交换（ZlgEtherCat.EnablePi）。\n通过统一输出所有者提交，禁止覆盖已绑定轴区域；提交不是设备动作确认。 执行时必须已确认 OP；StartBus 的 RequireDc=true 时还须确认 DC 内部同步与传播延迟初始化。 任一轴绑定了目标从站时，拒绝该从站的全部原始输出写入；其他从站的输入读取不受此门禁限制。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。",
        PdoWriteOperation.WritePdoMasked => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：使能过程映像交换（ZlgEtherCat.EnablePi）。\n 执行时必须已确认 OP；StartBus 的 RequireDc=true 时还须确认 DC 内部同步与传播延迟初始化。 任一轴绑定了目标从站时，拒绝该从站的全部原始输出写入；其他从站的输入读取不受此门禁限制。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(PdoWriteSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        PdoWriteOperation.WritePdo => "连接名",
        PdoWriteOperation.WritePdoMasked => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        PdoWriteOperation.WritePdo => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        PdoWriteOperation.WritePdoMasked => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; NumericValueChanged(nameof(SlaveIndex)); Changed(); }
    }
    public string SlaveIndexText { get => GetNumericText(nameof(SlaveIndex), Model.SlaveIndex); set => SetIntegerText(nameof(SlaveIndex), value, parsed => SlaveIndex = parsed); }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(PdoWriteSetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        PdoWriteOperation.WritePdo => "从站 ENI 索引（常量）",
        PdoWriteOperation.WritePdoMasked => "从站 ENI 索引（常量）",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        PdoWriteOperation.WritePdo => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        PdoWriteOperation.WritePdoMasked => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        _ => ""
    };

    public string SlaveIndexExpression
    {
        get => Model.SlaveIndexExpression;
        set { if (Equals(Model.SlaveIndexExpression, value)) return; Model.SlaveIndexExpression = value; Changed(); }
    }
    public bool IsSlaveIndexExpressionActive => Model.ActiveFields.Contains(nameof(PdoWriteSetting.SlaveIndexExpression));
    public Visibility SlaveIndexExpressionVisibility => IsSlaveIndexExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexExpressionLabel => Operation switch
    {
        PdoWriteOperation.WritePdo => "从站 ENI 索引表达式（可选）",
        PdoWriteOperation.WritePdoMasked => "从站 ENI 索引表达式（可选）",
        _ => ""
    };
    public string SlaveIndexExpressionInfo => Operation switch
    {
        PdoWriteOperation.WritePdo => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        PdoWriteOperation.WritePdoMasked => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        _ => ""
    };

    public int Offset
    {
        get => Model.Offset;
        set { if (Equals(Model.Offset, value)) return; Model.Offset = value; NumericValueChanged(nameof(Offset)); Changed(); }
    }
    public string OffsetText { get => GetNumericText(nameof(Offset), Model.Offset); set => SetIntegerText(nameof(Offset), value, parsed => Offset = parsed); }
    public bool IsOffsetActive => Model.ActiveFields.Contains(nameof(PdoWriteSetting.Offset));
    public Visibility OffsetVisibility => IsOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string OffsetLabel => Operation switch
    {
        PdoWriteOperation.WritePdo => "从站输出区相对字节偏移",
        PdoWriteOperation.WritePdoMasked => "从站输出区相对字节偏移",
        _ => ""
    };
    public string OffsetInfo => Operation switch
    {
        PdoWriteOperation.WritePdo => "相对指定从站输出映像起点，从 0 开始。不是全局 Offset，也不是对象索引；实际绝对位置=ReadPiRange 中输出 Offset+此值。 整数支持十进制和 0x 十六进制。 范围：0～16777215。",
        PdoWriteOperation.WritePdoMasked => "相对指定从站输出映像起点，从 0 开始。不是全局 Offset，也不是对象索引；实际绝对位置=ReadPiRange 中输出 Offset+此值。 整数支持十进制和 0x 十六进制。 范围：0～16777215。",
        _ => ""
    };

    public PdoDataType DataType
    {
        get => Model.DataType;
        set { if (Equals(Model.DataType, value)) return; Model.DataType = value; Changed(); }
    }
    public IReadOnlyList<KeyValuePair<PdoDataType, string>> DataTypeChoices => [new(PdoDataType.Bytes, "Bytes — 原始 byte[]，长度按配置，不转换"), new(PdoDataType.Boolean, "Boolean — bool，1 字节；任一位非零为 true，不是单个位"), new(PdoDataType.UInt8, "UInt8 — byte，无符号 8 位，1 字节"), new(PdoDataType.Int8, "Int8 — sbyte，有符号 8 位，1 字节"), new(PdoDataType.UInt16, "UInt16 — ushort，无符号 16 位，2 字节"), new(PdoDataType.Int16, "Int16 — short，有符号 16 位，2 字节"), new(PdoDataType.UInt32, "UInt32 — uint，无符号 32 位，4 字节"), new(PdoDataType.Int32, "Int32 — int，有符号 32 位，4 字节"), new(PdoDataType.UInt64, "UInt64 — ulong，无符号 64 位，8 字节"), new(PdoDataType.Int64, "Int64 — long，有符号 64 位，8 字节"), new(PdoDataType.Float32, "Float32 — float，IEEE 754，4 字节"), new(PdoDataType.Float64, "Float64 — double，IEEE 754，8 字节")];
    public bool IsDataTypeActive => Model.ActiveFields.Contains(nameof(PdoWriteSetting.DataType));
    public Visibility DataTypeVisibility => IsDataTypeActive ? Visibility.Visible : Visibility.Collapsed;
    public string DataTypeLabel => Operation switch
    {
        PdoWriteOperation.WritePdo => "写入数据类型",
        PdoWriteOperation.WritePdoMasked => "写入数据类型",
        _ => ""
    };
    public string DataTypeInfo => Operation switch
    {
        PdoWriteOperation.WritePdo => "可选值：Bytes — 原始 byte[]，长度按配置，不转换；Boolean — bool，1 字节；任一位非零为 true，不是单个位；UInt8 — byte，无符号 8 位，1 字节；Int8 — sbyte，有符号 8 位，1 字节；UInt16 — ushort，无符号 16 位，2 字节；Int16 — short，有符号 16 位，2 字节；UInt32 — uint，无符号 32 位，4 字节；Int32 — int，有符号 32 位，4 字节；UInt64 — ulong，无符号 64 位，8 字节；Int64 — long，有符号 64 位，8 字节；Float32 — float，IEEE 754，4 字节；Float64 — double，IEEE 754，8 字节。多字节固定小端；没有自动缩放。单个位请用位读取/数字输入步骤。 ENI 提供字段元数据时会核对标量类型、宽度与边界；Bytes 是原始访问，不核对对象类型。非字节对齐字段须用位步骤，不能选普通标量。",
        PdoWriteOperation.WritePdoMasked => "可选值：Bytes — 原始 byte[]，长度按配置，不转换；Boolean — bool，1 字节；任一位非零为 true，不是单个位；UInt8 — byte，无符号 8 位，1 字节；Int8 — sbyte，有符号 8 位，1 字节；UInt16 — ushort，无符号 16 位，2 字节；Int16 — short，有符号 16 位，2 字节；UInt32 — uint，无符号 32 位，4 字节；Int32 — int，有符号 32 位，4 字节；UInt64 — ulong，无符号 64 位，8 字节；Int64 — long，有符号 64 位，8 字节；Float32 — float，IEEE 754，4 字节；Float64 — double，IEEE 754，8 字节。多字节固定小端；没有自动缩放。单个位请用位读取/数字输入步骤。 ENI 提供字段元数据时会核对标量类型、宽度与边界；Bytes 是原始访问，不核对对象类型。非字节对齐字段须用位步骤，不能选普通标量。",
        _ => ""
    };

    public string Data
    {
        get => Model.Data;
        set { if (Equals(Model.Data, value)) return; Model.Data = value; Changed(); }
    }
    public bool IsDataActive => Model.ActiveFields.Contains(nameof(PdoWriteSetting.Data));
    public Visibility DataVisibility => IsDataActive ? Visibility.Visible : Visibility.Collapsed;
    public string DataLabel => Operation switch
    {
        PdoWriteOperation.WritePdo => "写入数据",
        PdoWriteOperation.WritePdoMasked => "写入数据",
        _ => ""
    };
    public string DataInfo => Operation switch
    {
        PdoWriteOperation.WritePdo => "按 DataType 求值：Bytes 要求 byte[]；其余要求相应标量类型。标量按小端编码，整数溢出或类型不符报错，不截断；Bytes 原样写入。",
        PdoWriteOperation.WritePdoMasked => "按 DataType 求值：Bytes 要求 byte[]；其余要求相应标量类型。标量按小端编码，整数溢出或类型不符报错，不截断；Bytes 原样写入。",
        _ => ""
    };

    public string Mask
    {
        get => Model.Mask;
        set { if (Equals(Model.Mask, value)) return; Model.Mask = value; Changed(); }
    }
    public bool IsMaskActive => Model.ActiveFields.Contains(nameof(PdoWriteSetting.Mask));
    public Visibility MaskVisibility => IsMaskActive ? Visibility.Visible : Visibility.Collapsed;
    public string MaskLabel => Operation switch
    {
        PdoWriteOperation.WritePdoMasked => "位掩码",
        _ => ""
    };
    public string MaskInfo => Operation switch
    {
        PdoWriteOperation.WritePdoMasked => "求值结果为 byte[]；长度必须与数据相同",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(PdoWriteSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        PdoWriteOperation.WritePdo => "步骤总超时（ms）",
        PdoWriteOperation.WritePdoMasked => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        PdoWriteOperation.WritePdo => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        PdoWriteOperation.WritePdoMasked => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(PdoWriteSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        PdoWriteOperation.WritePdo => "结果变量",
        PdoWriteOperation.WritePdoMasked => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        PdoWriteOperation.WritePdo => "可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        PdoWriteOperation.WritePdoMasked => "可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
