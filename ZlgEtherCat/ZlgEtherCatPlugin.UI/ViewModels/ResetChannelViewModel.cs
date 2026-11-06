using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ResetChannelViewModel : ZlgViewModelBase<ResetChannelSetting>
{
    public ResetChannelOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ResetChannelOperation, string>> OperationChoices => [new(ResetChannelOperation.ResetChannel, "复位主站通道")];
    public string OperationTitle => Operation switch
    {
        ResetChannelOperation.ResetChannel => "复位主站通道",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ResetChannelOperation.ResetChannel => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n仅停机维护时使用；不是普通停轴操作。 要求连接内无绑定轴、两类命名读取池均已关闭、采集和周期输出已停止、过程映像交换已禁用。 主站总线必须已停止。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ResetChannelSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ResetChannelOperation.ResetChannel => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ResetChannelOperation.ResetChannel => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ResetChannelSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ResetChannelOperation.ResetChannel => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ResetChannelOperation.ResetChannel => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ResetChannelSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ResetChannelOperation.ResetChannel => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ResetChannelOperation.ResetChannel => "可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
