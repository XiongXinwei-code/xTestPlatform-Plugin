using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class WriteSlaveEepromViewModel : ZlgViewModelBase<WriteSlaveEepromSetting>
{
    public WriteSlaveEepromOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<WriteSlaveEepromOperation, string>> OperationChoices => [new(WriteSlaveEepromOperation.WriteSlaveEeprom, "写入从站 EEPROM")];
    public string OperationTitle => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "写入从站 EEPROM",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n仅停机维护使用，可能影响从站身份与启动。 要求总线已经启动且处于 PREOP 或 BOOT、过程映像交换禁用；不等于 StopBus 后使用。 本操作要求整个连接没有任何绑定轴；即使目标是其他从站，也须先停轴、去使能并解绑。 要求连接内无绑定轴、两类命名读取池均已关闭、采集和周期输出已停止、过程映像交换已禁用。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(WriteSlaveEepromSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; NumericValueChanged(nameof(SlaveIndex)); Changed(); }
    }
    public string SlaveIndexText { get => GetNumericText(nameof(SlaveIndex), Model.SlaveIndex); set => SetIntegerText(nameof(SlaveIndex), value, parsed => SlaveIndex = parsed); }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(WriteSlaveEepromSetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "从站 ENI 索引（常量）",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        _ => ""
    };

    public string SlaveIndexExpression
    {
        get => Model.SlaveIndexExpression;
        set { if (Equals(Model.SlaveIndexExpression, value)) return; Model.SlaveIndexExpression = value; Changed(); }
    }
    public bool IsSlaveIndexExpressionActive => Model.ActiveFields.Contains(nameof(WriteSlaveEepromSetting.SlaveIndexExpression));
    public Visibility SlaveIndexExpressionVisibility => IsSlaveIndexExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexExpressionLabel => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "从站 ENI 索引表达式（可选）",
        _ => ""
    };
    public string SlaveIndexExpressionInfo => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        _ => ""
    };

    public int Address
    {
        get => Model.Address;
        set { if (Equals(Model.Address, value)) return; Model.Address = value; NumericValueChanged(nameof(Address)); Changed(); }
    }
    public string AddressText { get => GetNumericText(nameof(Address), Model.Address); set => SetIntegerText(nameof(Address), value, parsed => Address = parsed); }
    public bool IsAddressActive => Model.ActiveFields.Contains(nameof(WriteSlaveEepromSetting.Address));
    public Visibility AddressVisibility => IsAddressActive ? Visibility.Visible : Visibility.Collapsed;
    public string AddressLabel => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "起始地址",
        _ => ""
    };
    public string AddressInfo => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "原样传给 SDK u16StartAdr 的 EEPROM 地址；当前 SDK 头未注明地址计数单位，须按厂商接口说明确认，不能套用 PDO 字节偏移 整数支持十进制和 0x 十六进制。 范围：0～65535。",
        _ => ""
    };

    public string Data
    {
        get => Model.Data;
        set { if (Equals(Model.Data, value)) return; Model.Data = value; Changed(); }
    }
    public bool IsDataActive => Model.ActiveFields.Contains(nameof(WriteSlaveEepromSetting.Data));
    public Visibility DataVisibility => IsDataActive ? Visibility.Visible : Visibility.Collapsed;
    public string DataLabel => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "写入数据",
        _ => ""
    };
    public string DataInfo => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "求值结果为 byte[]；多字节按小端编码",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(WriteSlaveEepromSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(WriteSlaveEepromSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        WriteSlaveEepromOperation.WriteSlaveEeprom => "可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
