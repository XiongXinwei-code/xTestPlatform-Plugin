using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ConfigureAutoClearViewModel : ZlgViewModelBase<ConfigureAutoClearSetting>
{
    public ConfigureAutoClearOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ConfigureAutoClearOperation, string>> OperationChoices => [new(ConfigureAutoClearOperation.ConfigureAutoClear, "配置自动清错")];
    public string OperationTitle => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => "配置自动清错",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n 本操作要求整个连接没有任何绑定轴；即使目标是其他从站，也须先停轴、去使能并解绑。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：bool：读取回来的自动清错使能状态。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ConfigureAutoClearSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public bool Enabled
    {
        get => Model.Enabled;
        set { if (Equals(Model.Enabled, value)) return; Model.Enabled = value; Changed(); }
    }
    public bool IsEnabledActive => Model.ActiveFields.Contains(nameof(ConfigureAutoClearSetting.Enabled));
    public Visibility EnabledVisibility => IsEnabledActive ? Visibility.Visible : Visibility.Collapsed;
    public string EnabledLabel => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => "自动清错使能",
        _ => ""
    };
    public string EnabledInfo => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => "",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ConfigureAutoClearSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ConfigureAutoClearSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ConfigureAutoClearOperation.ConfigureAutoClear => "可选已声明变量完整路径；bool：读取回来的自动清错使能状态。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
