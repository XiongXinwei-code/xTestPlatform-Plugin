using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ConfigureExchangeViewModel : ZlgViewModelBase<ConfigureExchangeSetting>
{
    public ConfigureExchangeOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ConfigureExchangeOperation, string>> OperationChoices => [new(ConfigureExchangeOperation.ConfigureExchange, "配置 PDO 交换")];
    public string OperationTitle => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "配置 PDO 交换",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：加载 ENI 并启动主站（ZlgEtherCat.StartBus）。\n运行中禁止切换通路；映像、队列与所有输出由单一通道运行时管理。 要求连接内无绑定轴、两类命名读取池均已关闭、采集和周期输出已停止、过程映像交换已禁用。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：对象：Mode（实际配置的交换模式）、OutputBytes（完整主站输出字节数）。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ConfigureExchangeSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public ExchangeMode ExchangeMode
    {
        get => Model.ExchangeMode;
        set { if (Equals(Model.ExchangeMode, value)) return; Model.ExchangeMode = value; Changed(); }
    }
    public IReadOnlyList<KeyValuePair<ExchangeMode, string>> ExchangeModeChoices => [new(ExchangeMode.Queue, "Queue — SDK 输入/输出队列；运动 PDO 必须选此项"), new(ExchangeMode.Direct, "Direct — 直接过程映像访问，无新队列帧证明"), new(ExchangeMode.Cycle, "Cycle — SDK 周期异步映像，无新队列帧证明"), new(ExchangeMode.Manual, "Manual — 显式更新异步映像，无新队列帧证明")];
    public bool IsExchangeModeActive => Model.ActiveFields.Contains(nameof(ConfigureExchangeSetting.ExchangeMode));
    public Visibility ExchangeModeVisibility => IsExchangeModeActive ? Visibility.Visible : Visibility.Collapsed;
    public string ExchangeModeLabel => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "交换通路",
        _ => ""
    };
    public string ExchangeModeInfo => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "可选值：Queue — SDK 输入/输出队列；运动 PDO 必须选此项；Direct — 直接过程映像访问，无新队列帧证明；Cycle — SDK 周期异步映像，无新队列帧证明；Manual — 显式更新异步映像，无新队列帧证明",
        _ => ""
    };

    public int QueuePrefill
    {
        get => Model.QueuePrefill;
        set { if (Equals(Model.QueuePrefill, value)) return; Model.QueuePrefill = value; NumericValueChanged(nameof(QueuePrefill)); Changed(); }
    }
    public string QueuePrefillText { get => GetNumericText(nameof(QueuePrefill), Model.QueuePrefill); set => SetIntegerText(nameof(QueuePrefill), value, parsed => QueuePrefill = parsed); }
    public bool IsQueuePrefillActive => Model.ActiveFields.Contains(nameof(ConfigureExchangeSetting.QueuePrefill));
    public Visibility QueuePrefillVisibility => IsQueuePrefillActive ? Visibility.Visible : Visibility.Collapsed;
    public string QueuePrefillLabel => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "输出队列预填数量",
        _ => ""
    };
    public string QueuePrefillInfo => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "仅 ExchangeMode=Queue 生效，填入完整初始输出映像的队列帧数；受真实 SDK 队列容量限制，不是毫秒 整数支持十进制和 0x 十六进制。 范围：0～4096。",
        _ => ""
    };

    public string InitialOutput
    {
        get => Model.InitialOutput;
        set { if (Equals(Model.InitialOutput, value)) return; Model.InitialOutput = value; Changed(); }
    }
    public bool IsInitialOutputActive => Model.ActiveFields.Contains(nameof(ConfigureExchangeSetting.InitialOutput));
    public Visibility InitialOutputVisibility => IsInitialOutputActive ? Visibility.Visible : Visibility.Collapsed;
    public string InitialOutputLabel => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "初始输出",
        _ => ""
    };
    public string InitialOutputInfo => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "整个主站输出映像的 byte[] 初值，长度必须等于 ReadEniInfo.OutputBytes；不是单个从站输出，不能假定全零安全",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ConfigureExchangeSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ConfigureExchangeSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ConfigureExchangeOperation.ConfigureExchange => "可选已声明变量完整路径；对象：Mode（实际配置的交换模式）、OutputBytes（完整主站输出字节数）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
