using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class EnumerateSlavesViewModel : ZlgViewModelBase<EnumerateSlavesSetting>
{
    public EnumerateSlavesOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<EnumerateSlavesOperation, string>> OperationChoices => [new(EnumerateSlavesOperation.EnumerateSlaves, "枚举从站")];
    public string OperationTitle => Operation switch
    {
        EnumerateSlavesOperation.EnumerateSlaves => "枚举从站",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        EnumerateSlavesOperation.EnumerateSlaves => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：加载 ENI 并启动主站（ZlgEtherCat.StartBus）。\n\n返回：从站数组：Index/SlaveIndex（本 ENI 的零基 SlaveIdx）、Active、Ready、FixedAddress、Info（可为 null）、Range、InfoSource、FixedAddressSource。FixedAddress 来源是当前 ENI；SDK 身份信息未独立验证在线物理身份；离线项仍保留其索引。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(EnumerateSlavesSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        EnumerateSlavesOperation.EnumerateSlaves => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        EnumerateSlavesOperation.EnumerateSlaves => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(EnumerateSlavesSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        EnumerateSlavesOperation.EnumerateSlaves => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        EnumerateSlavesOperation.EnumerateSlaves => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(EnumerateSlavesSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        EnumerateSlavesOperation.EnumerateSlaves => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        EnumerateSlavesOperation.EnumerateSlaves => "可选已声明变量完整路径；从站数组：Index/SlaveIndex（本 ENI 的零基 SlaveIdx）、Active、Ready、FixedAddress、Info（可为 null）、Range、InfoSource、FixedAddressSource。FixedAddress 来源是当前 ENI；SDK 身份信息未独立验证在线物理身份；离线项仍保留其索引。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
