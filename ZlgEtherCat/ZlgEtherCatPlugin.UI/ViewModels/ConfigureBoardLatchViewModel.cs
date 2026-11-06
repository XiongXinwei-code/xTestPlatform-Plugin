using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ConfigureBoardLatchViewModel : ZlgViewModelBase<ConfigureBoardLatchSetting>
{
    public ConfigureBoardLatchOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ConfigureBoardLatchOperation, string>> OperationChoices => [new(ConfigureBoardLatchOperation.ConfigureBoardLatch, "配置板载编码器锁存")];
    public string OperationTitle => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "配置板载编码器锁存",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n 本操作要求整个连接没有任何绑定轴；即使目标是其他从站，也须先停轴、去使能并解绑。 板载 PWM/脉冲/锁存共享整板归属；其他通道持有归属时拒绝操作。输出启动后需显式停止，锁存配置归属保持到通道关闭。 锁存运行中或停止结果未确认时，必须先成功执行 StopBoardLatch。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ConfigureBoardLatchSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int PortSelection
    {
        get => Model.PortSelection;
        set { if (Equals(Model.PortSelection, value)) return; Model.PortSelection = value; NumericValueChanged(nameof(PortSelection)); Changed(); }
    }
    public string PortSelectionText { get => GetNumericText(nameof(PortSelection), Model.PortSelection); set => SetIntegerText(nameof(PortSelection), value, parsed => PortSelection = parsed); }
    public bool IsPortSelectionActive => Model.ActiveFields.Contains(nameof(ConfigureBoardLatchSetting.PortSelection));
    public Visibility PortSelectionVisibility => IsPortSelectionActive ? Visibility.Visible : Visibility.Collapsed;
    public string PortSelectionLabel => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "引脚选择位图",
        _ => ""
    };
    public string PortSelectionInfo => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => " 整数支持十进制和 0x 十六进制。 范围：0～255。",
        _ => ""
    };

    public int LatchMode
    {
        get => Model.LatchMode;
        set { if (Equals(Model.LatchMode, value)) return; Model.LatchMode = value; NumericValueChanged(nameof(LatchMode)); Changed(); }
    }
    public IReadOnlyList<KeyValuePair<int, string>> LatchModeChoices => [new(1, "1 — 单次 Z上升→A"), new(2, "2 — 单次 Z下降→A"), new(3, "3 — 单次 R0上升→A"), new(4, "4 — 单次 R0下降→A"), new(6, "6 — 单次 R0上升→A + Z上升→B"), new(7, "7 — 单次 R0上升→A + Z下降→B"), new(8, "8 — 单次 R0下降→A + Z上升→B"), new(9, "9 — 单次 R0下降→A + Z下降→B"), new(10, "10 — 单次 R0上升→A + R1上升→B"), new(11, "11 — 单次 R0上升→A + R1下降→B"), new(12, "12 — 单次 R0下降→A + R1上升→B"), new(13, "13 — 单次 R0下降→A + R1下降→B"), new(14, "14 — 单次 R1上升→B"), new(15, "15 — 单次 R1下降→B"), new(16, "16 — 单次 Z上升→B"), new(17, "17 — 单次 Z下降→B"), new(18, "18 — 单次 R2上升→C"), new(19, "19 — 单次 R2下降→C"), new(20, "20 — 单次 R3上升→D"), new(21, "21 — 单次 R3下降→D"), new(101, "101 — 连续 Z上升→A"), new(102, "102 — 连续 Z下降→A"), new(103, "103 — 连续 R0上升→A"), new(104, "104 — 连续 R0下降→A"), new(114, "114 — 连续 R1上升→B"), new(115, "115 — 连续 R1下降→B"), new(116, "116 — 连续 Z上升→B"), new(117, "117 — 连续 Z下降→B"), new(118, "118 — 连续 R2上升→C"), new(119, "119 — 连续 R2下降→C"), new(120, "120 — 连续 R3上升→D"), new(121, "121 — 连续 R3下降→D"), new(123, "123 — 连续 R0上升→B"), new(124, "124 — 连续 R0下降→B"), new(133, "133 — 连续 R0先上后下→A"), new(134, "134 — 连续 R0先下后上→A"), new(135, "135 — 连续 R1先上后下→B"), new(136, "136 — 连续 R1先下后上→B")];
    public bool IsLatchModeActive => Model.ActiveFields.Contains(nameof(ConfigureBoardLatchSetting.LatchMode));
    public Visibility LatchModeVisibility => IsLatchModeActive ? Visibility.Visible : Visibility.Collapsed;
    public string LatchModeLabel => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "厂商锁存模式",
        _ => ""
    };
    public string LatchModeInfo => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "必须与 Continuous 一致；列表中的单次/连续是不同 SDK 模式，配置不匹配会被拒绝 范围：1～136。",
        _ => ""
    };

    public bool Continuous
    {
        get => Model.Continuous;
        set { if (Equals(Model.Continuous, value)) return; Model.Continuous = value; Changed(); }
    }
    public bool IsContinuousActive => Model.ActiveFields.Contains(nameof(ConfigureBoardLatchSetting.Continuous));
    public Visibility ContinuousVisibility => IsContinuousActive ? Visibility.Visible : Visibility.Collapsed;
    public string ContinuousLabel => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "连续锁存",
        _ => ""
    };
    public string ContinuousInfo => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ConfigureBoardLatchSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ConfigureBoardLatchSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ConfigureBoardLatchOperation.ConfigureBoardLatch => "可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
