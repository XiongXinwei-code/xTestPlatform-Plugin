using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class WriteSlaveMemoryViewModel : ZlgViewModelBase<WriteSlaveMemorySetting>
{
    public WriteSlaveMemoryOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<WriteSlaveMemoryOperation, string>> OperationChoices => [new(WriteSlaveMemoryOperation.WriteSlaveMemory, "写入 ESC 内存")];
    public string OperationTitle => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "写入 ESC 内存",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n仅停机维护使用，不自动恢复或重试。 要求总线已经启动且处于 PREOP 或 BOOT、过程映像交换禁用；不等于 StopBus 后使用。 本操作要求整个连接没有任何绑定轴；即使目标是其他从站，也须先停轴、去使能并解绑。 要求连接内无绑定轴、两类命名读取池均已关闭、采集和周期输出已停止、过程映像交换已禁用。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(WriteSlaveMemorySetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; NumericValueChanged(nameof(SlaveIndex)); Changed(); }
    }
    public string SlaveIndexText { get => GetNumericText(nameof(SlaveIndex), Model.SlaveIndex); set => SetIntegerText(nameof(SlaveIndex), value, parsed => SlaveIndex = parsed); }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(WriteSlaveMemorySetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "从站 ENI 索引（常量）",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        _ => ""
    };

    public string SlaveIndexExpression
    {
        get => Model.SlaveIndexExpression;
        set { if (Equals(Model.SlaveIndexExpression, value)) return; Model.SlaveIndexExpression = value; Changed(); }
    }
    public bool IsSlaveIndexExpressionActive => Model.ActiveFields.Contains(nameof(WriteSlaveMemorySetting.SlaveIndexExpression));
    public Visibility SlaveIndexExpressionVisibility => IsSlaveIndexExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexExpressionLabel => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "从站 ENI 索引表达式（可选）",
        _ => ""
    };
    public string SlaveIndexExpressionInfo => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        _ => ""
    };

    public int Address
    {
        get => Model.Address;
        set { if (Equals(Model.Address, value)) return; Model.Address = value; NumericValueChanged(nameof(Address)); Changed(); }
    }
    public string AddressText { get => GetNumericText(nameof(Address), Model.Address); set => SetIntegerText(nameof(Address), value, parsed => Address = parsed); }
    public bool IsAddressActive => Model.ActiveFields.Contains(nameof(WriteSlaveMemorySetting.Address));
    public Visibility AddressVisibility => IsAddressActive ? Visibility.Visible : Visibility.Collapsed;
    public string AddressLabel => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "ESC 地址",
        _ => ""
    };
    public string AddressInfo => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "ESC 寄存器起始字节地址，支持十进制/0x；不是 PDO 映像偏移或 CoE 对象索引 整数支持十进制和 0x 十六进制。 范围：0～65535。",
        _ => ""
    };

    public string Data
    {
        get => Model.Data;
        set { if (Equals(Model.Data, value)) return; Model.Data = value; Changed(); }
    }
    public bool IsDataActive => Model.ActiveFields.Contains(nameof(WriteSlaveMemorySetting.Data));
    public Visibility DataVisibility => IsDataActive ? Visibility.Visible : Visibility.Collapsed;
    public string DataLabel => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "写入数据",
        _ => ""
    };
    public string DataInfo => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "求值结果为 byte[]；多字节按小端编码",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(WriteSlaveMemorySetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(WriteSlaveMemorySetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        WriteSlaveMemoryOperation.WriteSlaveMemory => "可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
