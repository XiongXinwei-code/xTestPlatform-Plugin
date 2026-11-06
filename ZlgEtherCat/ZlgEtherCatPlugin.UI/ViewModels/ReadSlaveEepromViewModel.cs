using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ReadSlaveEepromViewModel : ZlgViewModelBase<ReadSlaveEepromSetting>
{
    public ReadSlaveEepromOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ReadSlaveEepromOperation, string>> OperationChoices => [new(ReadSlaveEepromOperation.ReadSlaveEeprom, "读取从站 EEPROM")];
    public string OperationTitle => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "读取从站 EEPROM",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n 要求总线已经启动且处于 PREOP 或 BOOT、过程映像交换禁用；不等于 StopBus 后使用。 Queue 周期输出运行期间拒绝本操作，避免长邮箱/维护调用阻塞唯一 SDK 线程；需先完成停轴、去使能、解绑并停止周期输出。\n返回：byte[]：指定 EEPROM 地址范围的原始数据；地址单位依 SDK/设备定义。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ReadSlaveEepromSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; NumericValueChanged(nameof(SlaveIndex)); Changed(); }
    }
    public string SlaveIndexText { get => GetNumericText(nameof(SlaveIndex), Model.SlaveIndex); set => SetIntegerText(nameof(SlaveIndex), value, parsed => SlaveIndex = parsed); }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(ReadSlaveEepromSetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "从站 ENI 索引（常量）",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        _ => ""
    };

    public string SlaveIndexExpression
    {
        get => Model.SlaveIndexExpression;
        set { if (Equals(Model.SlaveIndexExpression, value)) return; Model.SlaveIndexExpression = value; Changed(); }
    }
    public bool IsSlaveIndexExpressionActive => Model.ActiveFields.Contains(nameof(ReadSlaveEepromSetting.SlaveIndexExpression));
    public Visibility SlaveIndexExpressionVisibility => IsSlaveIndexExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexExpressionLabel => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "从站 ENI 索引表达式（可选）",
        _ => ""
    };
    public string SlaveIndexExpressionInfo => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        _ => ""
    };

    public int Address
    {
        get => Model.Address;
        set { if (Equals(Model.Address, value)) return; Model.Address = value; NumericValueChanged(nameof(Address)); Changed(); }
    }
    public string AddressText { get => GetNumericText(nameof(Address), Model.Address); set => SetIntegerText(nameof(Address), value, parsed => Address = parsed); }
    public bool IsAddressActive => Model.ActiveFields.Contains(nameof(ReadSlaveEepromSetting.Address));
    public Visibility AddressVisibility => IsAddressActive ? Visibility.Visible : Visibility.Collapsed;
    public string AddressLabel => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "起始地址",
        _ => ""
    };
    public string AddressInfo => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "原样传给 SDK u16StartAdr 的 EEPROM 地址；当前 SDK 头未注明地址计数单位，须按厂商接口说明确认，不能套用 PDO 字节偏移 整数支持十进制和 0x 十六进制。 范围：0～65535。",
        _ => ""
    };

    public int Length
    {
        get => Model.Length;
        set { if (Equals(Model.Length, value)) return; Model.Length = value; NumericValueChanged(nameof(Length)); Changed(); }
    }
    public string LengthText { get => GetNumericText(nameof(Length), Model.Length); set => SetIntegerText(nameof(Length), value, parsed => Length = parsed); }
    public bool IsLengthActive => Model.ActiveFields.Contains(nameof(ReadSlaveEepromSetting.Length));
    public Visibility LengthVisibility => IsLengthActive ? Visibility.Visible : Visibility.Collapsed;
    public string LengthLabel => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "长度",
        _ => ""
    };
    public string LengthInfo => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "本次读取的字节数，SDK DataLen 以字节计；不要把它当作地址单位或寄存器个数 整数支持十进制和 0x 十六进制。 范围：1～65535。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ReadSlaveEepromSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ReadSlaveEepromSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ReadSlaveEepromOperation.ReadSlaveEeprom => "可选已声明变量完整路径；byte[]：指定 EEPROM 地址范围的原始数据；地址单位依 SDK/设备定义。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
