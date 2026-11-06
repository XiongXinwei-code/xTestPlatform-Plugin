using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class OpenViewModel : ZlgViewModelBase<OpenSetting>
{
    public OpenOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<OpenOperation, string>> OperationChoices => [new(OpenOperation.Open, "打开板卡通道")];
    public string OperationTitle => Operation switch
    {
        OpenOperation.Open => "打开板卡通道",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        OpenOperation.Open => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：无。\n以 ConnectionName 注册连接；同名或同物理通道重复打开报错，不静默替换。\n返回：已选板卡：Index 与 Info（Name、Version、SerialNumber、Alias、ChannelCount、FpgaVersion）。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(OpenSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        OpenOperation.Open => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        OpenOperation.Open => "本插件连接会话名称表达式；用于后续步骤选择同一板卡通道，不是板卡序列号或从站编号",
        _ => ""
    };

    public string BoardSelector
    {
        get => Model.BoardSelector;
        set { if (Equals(Model.BoardSelector, value)) return; Model.BoardSelector = value; Changed(); }
    }
    public bool IsBoardSelectorActive => Model.ActiveFields.Contains(nameof(OpenSetting.BoardSelector));
    public Visibility BoardSelectorVisibility => IsBoardSelectorActive ? Visibility.Visible : Visibility.Collapsed;
    public string BoardSelectorLabel => Operation switch
    {
        OpenOperation.Open => "板卡标识",
        _ => ""
    };
    public string BoardSelectorInfo => Operation switch
    {
        OpenOperation.Open => "求值结果为 string；SDK ID索引、AL别名或NO序列号",
        _ => ""
    };

    public int Channel
    {
        get => Model.Channel;
        set { if (Equals(Model.Channel, value)) return; Model.Channel = value; NumericValueChanged(nameof(Channel)); Changed(); }
    }
    public string ChannelText { get => GetNumericText(nameof(Channel), Model.Channel); set => SetIntegerText(nameof(Channel), value, parsed => Channel = parsed); }
    public bool IsChannelActive => Model.ActiveFields.Contains(nameof(OpenSetting.Channel));
    public Visibility ChannelVisibility => IsChannelActive ? Visibility.Visible : Visibility.Collapsed;
    public string ChannelLabel => Operation switch
    {
        OpenOperation.Open => "通道号",
        _ => ""
    };
    public string ChannelInfo => Operation switch
    {
        OpenOperation.Open => " 整数支持十进制和 0x 十六进制。 范围：0～255。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(OpenSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        OpenOperation.Open => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        OpenOperation.Open => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(OpenSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        OpenOperation.Open => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        OpenOperation.Open => "可选已声明变量完整路径；已选板卡：Index 与 Info（Name、Version、SerialNumber、Alias、ChannelCount、FpgaVersion）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
