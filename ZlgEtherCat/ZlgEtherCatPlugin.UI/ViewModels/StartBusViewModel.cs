using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class StartBusViewModel : ZlgViewModelBase<StartBusSetting>
{
    public StartBusOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<StartBusOperation, string>> OperationChoices => [new(StartBusOperation.StartBus, "加载 ENI 并启动主站")];
    public string OperationTitle => Operation switch
    {
        StartBusOperation.StartBus => "加载 ENI 并启动主站",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        StartBusOperation.StartBus => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：配置映像初始化策略（ZlgEtherCat.ConfigureReinit）。\nDC 和总线周期由 ENI 配置，启动前检查与本步骤预期一致；不以 Windows 工作循环保证 250/500 μs 实时运动。启动前必须配置本次映像初始化策略；启动会复位 PI 使能及相关 SDK 配置。 要求连接内无绑定轴、两类命名读取池均已关闭、采集和周期输出已停止、过程映像交换已禁用。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：ENI 信息对象：MainCycle、MailboxCycle（μs）、OutputBytes、InputBytes（全主站字节数）。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(StartBusSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        StartBusOperation.StartBus => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        StartBusOperation.StartBus => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string EniPath
    {
        get => Model.EniPath;
        set { if (Equals(Model.EniPath, value)) return; Model.EniPath = value; Changed(); }
    }
    public bool IsEniPathActive => Model.ActiveFields.Contains(nameof(StartBusSetting.EniPath));
    public Visibility EniPathVisibility => IsEniPathActive ? Visibility.Visible : Visibility.Collapsed;
    public string EniPathLabel => Operation switch
    {
        StartBusOperation.StartBus => "ENI 文件路径",
        _ => ""
    };
    public string EniPathInfo => Operation switch
    {
        StartBusOperation.StartBus => "求值结果为 string；现有本地 ENI 文件",
        _ => ""
    };

    public int ExpectedCycleUs
    {
        get => Model.ExpectedCycleUs;
        set { if (Equals(Model.ExpectedCycleUs, value)) return; Model.ExpectedCycleUs = value; NumericValueChanged(nameof(ExpectedCycleUs)); Changed(); }
    }
    public string ExpectedCycleUsText { get => GetNumericText(nameof(ExpectedCycleUs), Model.ExpectedCycleUs); set => SetIntegerText(nameof(ExpectedCycleUs), value, parsed => ExpectedCycleUs = parsed); }
    public bool IsExpectedCycleUsActive => Model.ActiveFields.Contains(nameof(StartBusSetting.ExpectedCycleUs));
    public Visibility ExpectedCycleUsVisibility => IsExpectedCycleUsActive ? Visibility.Visible : Visibility.Collapsed;
    public string ExpectedCycleUsLabel => Operation switch
    {
        StartBusOperation.StartBus => "预期总线周期（μs）",
        _ => ""
    };
    public string ExpectedCycleUsInfo => Operation switch
    {
        StartBusOperation.StartBus => "与 ENI 中的总线周期一致；HPCIe-2E 目标为 250 μs 或 500 μs 整数支持十进制和 0x 十六进制。 范围：1～1000000。",
        _ => ""
    };

    public bool RequireDc
    {
        get => Model.RequireDc;
        set { if (Equals(Model.RequireDc, value)) return; Model.RequireDc = value; Changed(); }
    }
    public bool IsRequireDcActive => Model.ActiveFields.Contains(nameof(StartBusSetting.RequireDc));
    public Visibility RequireDcVisibility => IsRequireDcActive ? Visibility.Visible : Visibility.Collapsed;
    public string RequireDcLabel => Operation switch
    {
        StartBusOperation.StartBus => "要求 ENI 配置 DC",
        _ => ""
    };
    public string RequireDcInfo => Operation switch
    {
        StartBusOperation.StartBus => "",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(StartBusSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        StartBusOperation.StartBus => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        StartBusOperation.StartBus => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(StartBusSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        StartBusOperation.StartBus => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        StartBusOperation.StartBus => "可选已声明变量完整路径；ENI 信息对象：MainCycle、MailboxCycle（μs）、OutputBytes、InputBytes（全主站字节数）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
