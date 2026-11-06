using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ReadBoardLatchViewModel : ZlgViewModelBase<ReadBoardLatchSetting>
{
    public ReadBoardLatchOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ReadBoardLatchOperation, string>> OperationChoices => [new(ReadBoardLatchOperation.ReadBoardLatch, "读取板载锁存")];
    public string OperationTitle => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "读取板载锁存",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。\n前置步骤：启动板载锁存（ZlgEtherCat.StartBoardLatch）。\n仅在锁存已触发且 Continuous 与本连接配置匹配时读取。SampleIndex 必须小于启动时配置的容量；SDK 不提供逐样本已写入计数，返回不证明此槽在本次触发中已更新。 板载 PWM/脉冲/锁存共享整板归属；其他通道持有归属时拒绝操作。输出启动后需显式停止，锁存配置归属保持到通道关闭。\n返回：ulong：指定锁存通道/样本的原始计数值。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ReadBoardLatchSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int Port
    {
        get => Model.Port;
        set { if (Equals(Model.Port, value)) return; Model.Port = value; NumericValueChanged(nameof(Port)); Changed(); }
    }
    public string PortText { get => GetNumericText(nameof(Port), Model.Port); set => SetIntegerText(nameof(Port), value, parsed => Port = parsed); }
    public bool IsPortActive => Model.ActiveFields.Contains(nameof(ReadBoardLatchSetting.Port));
    public Visibility PortVisibility => IsPortActive ? Visibility.Visible : Visibility.Collapsed;
    public string PortLabel => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "锁存通道",
        _ => ""
    };
    public string PortInfo => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => " 整数支持十进制和 0x 十六进制。 范围：0～3。",
        _ => ""
    };

    public bool Continuous
    {
        get => Model.Continuous;
        set { if (Equals(Model.Continuous, value)) return; Model.Continuous = value; Changed(); }
    }
    public bool IsContinuousActive => Model.ActiveFields.Contains(nameof(ReadBoardLatchSetting.Continuous));
    public Visibility ContinuousVisibility => IsContinuousActive ? Visibility.Visible : Visibility.Collapsed;
    public string ContinuousLabel => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "连续锁存",
        _ => ""
    };
    public string ContinuousInfo => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "",
        _ => ""
    };

    public int SampleIndex
    {
        get => Model.SampleIndex;
        set { if (Equals(Model.SampleIndex, value)) return; Model.SampleIndex = value; NumericValueChanged(nameof(SampleIndex)); Changed(); }
    }
    public string SampleIndexText { get => GetNumericText(nameof(SampleIndex), Model.SampleIndex); set => SetIntegerText(nameof(SampleIndex), value, parsed => SampleIndex = parsed); }
    public bool IsSampleIndexActive => Model.ActiveFields.Contains(nameof(ReadBoardLatchSetting.SampleIndex));
    public Visibility SampleIndexVisibility => IsSampleIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SampleIndexLabel => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "记录样本索引",
        _ => ""
    };
    public string SampleIndexInfo => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "索引必须小于本连接启动锁存时配置的记录容量。连续模式读取指定硬件缓冲槽；触发后可读取，但不保证该槽本次已更新，不证明新样本 整数支持十进制和 0x 十六进制。 范围：0～1022。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ReadBoardLatchSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ReadBoardLatchSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ReadBoardLatchOperation.ReadBoardLatch => "可选已声明变量完整路径；ulong：指定锁存通道/样本的原始计数值。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
