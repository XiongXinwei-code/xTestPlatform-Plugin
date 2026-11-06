using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class FeedWatchdogViewModel : ZlgViewModelBase<FeedWatchdogSetting>
{
    public FeedWatchdogOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<FeedWatchdogOperation, string>> OperationChoices => [new(FeedWatchdogOperation.FeedWatchdog, "手动喂狗")];
    public string OperationTitle => Operation switch
    {
        FeedWatchdogOperation.FeedWatchdog => "手动喂狗",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        FeedWatchdogOperation.FeedWatchdog => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。 HPCIe-2E 官方能力表与通用 SDK 接口存在支持差异；本版本对 HPCIe-2E 系列拒绝全部五项看门狗操作，不能据此宣称已有硬件保护。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n仅显式执行时喂狗；后台输入读取不会自动喂狗，不以通信活跃代替工艺健康。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(FeedWatchdogSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        FeedWatchdogOperation.FeedWatchdog => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        FeedWatchdogOperation.FeedWatchdog => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(FeedWatchdogSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        FeedWatchdogOperation.FeedWatchdog => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        FeedWatchdogOperation.FeedWatchdog => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(FeedWatchdogSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        FeedWatchdogOperation.FeedWatchdog => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        FeedWatchdogOperation.FeedWatchdog => "可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
