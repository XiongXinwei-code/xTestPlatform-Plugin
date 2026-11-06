using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ConfigureReinitViewModel : ZlgViewModelBase<ConfigureReinitSetting>
{
    public ConfigureReinitOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ConfigureReinitOperation, string>> OperationChoices => [new(ConfigureReinitOperation.ConfigureReinit, "配置映像初始化策略")];
    public string OperationTitle => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => "配置映像初始化策略",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n本地模式在下一次 StartBus 时生效，不自动改变已运行总线。 要求连接内无绑定轴、两类命名读取池均已关闭、采集和周期输出已停止、过程映像交换已禁用。 主站总线必须已停止。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ConfigureReinitSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public PiReinitMode ReinitMode
    {
        get => Model.ReinitMode;
        set { if (Equals(Model.ReinitMode, value)) return; Model.ReinitMode = value; Changed(); }
    }
    public IReadOnlyList<KeyValuePair<PiReinitMode, string>> ReinitModeChoices => [new(PiReinitMode.Auto, "Auto — SDK 自动策略"), new(PiReinitMode.ClearAll, "ClearAll — 清空全部过程映像"), new(PiReinitMode.PreserveAsynchronous, "PreserveAsynchronous — 保留异步映像"), new(PiReinitMode.PreserveSynchronous, "PreserveSynchronous — 保留同步映像"), new(PiReinitMode.PreserveAll, "PreserveAll — 保留全部映像")];
    public bool IsReinitModeActive => Model.ActiveFields.Contains(nameof(ConfigureReinitSetting.ReinitMode));
    public Visibility ReinitModeVisibility => IsReinitModeActive ? Visibility.Visible : Visibility.Collapsed;
    public string ReinitModeLabel => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => "启动时策略",
        _ => ""
    };
    public string ReinitModeInfo => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => "可选值：Auto — SDK 自动策略；ClearAll — 清空全部过程映像；PreserveAsynchronous — 保留异步映像；PreserveSynchronous — 保留同步映像；PreserveAll — 保留全部映像",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ConfigureReinitSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ConfigureReinitSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ConfigureReinitOperation.ConfigureReinit => "可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
