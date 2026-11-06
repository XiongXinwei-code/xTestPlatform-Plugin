using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ConfigureReadyLostViewModel : ZlgViewModelBase<ConfigureReadyLostSetting>
{
    public ConfigureReadyLostOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ConfigureReadyLostOperation, string>> OperationChoices => [new(ConfigureReadyLostOperation.ConfigureReadyLost, "配置就绪丢失超时")];
    public string OperationTitle => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => "配置就绪丢失超时",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\nStartBus 后该配置复位，需要再次显式应用。 本操作要求整个连接没有任何绑定轴；即使目标是其他从站，也须先停轴、去使能并解绑。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：uint：读取回来的就绪丢失超时（ms）。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ConfigureReadyLostSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int ReadyLostTimeoutMs
    {
        get => Model.ReadyLostTimeoutMs;
        set { if (Equals(Model.ReadyLostTimeoutMs, value)) return; Model.ReadyLostTimeoutMs = value; NumericValueChanged(nameof(ReadyLostTimeoutMs)); Changed(); }
    }
    public string ReadyLostTimeoutMsText { get => GetNumericText(nameof(ReadyLostTimeoutMs), Model.ReadyLostTimeoutMs); set => SetIntegerText(nameof(ReadyLostTimeoutMs), value, parsed => ReadyLostTimeoutMs = parsed); }
    public bool IsReadyLostTimeoutMsActive => Model.ActiveFields.Contains(nameof(ConfigureReadyLostSetting.ReadyLostTimeoutMs));
    public Visibility ReadyLostTimeoutMsVisibility => IsReadyLostTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string ReadyLostTimeoutMsLabel => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => "就绪丢失超时（ms）",
        _ => ""
    };
    public string ReadyLostTimeoutMsInfo => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => " 整数支持十进制和 0x 十六进制。 范围：0～3600000。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ConfigureReadyLostSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ConfigureReadyLostSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ConfigureReadyLostOperation.ConfigureReadyLost => "可选已声明变量完整路径；uint：读取回来的就绪丢失超时（ms）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
