using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class StartCaptureViewModel : ZlgViewModelBase<StartCaptureSetting>
{
    public StartCaptureOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<StartCaptureOperation, string>> OperationChoices => [new(StartCaptureOperation.StartCapture, "启动 SDK 抓包")];
    public string OperationTitle => Operation switch
    {
        StartCaptureOperation.StartCapture => "启动 SDK 抓包",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        StartCaptureOperation.StartCapture => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(StartCaptureSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        StartCaptureOperation.StartCapture => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        StartCaptureOperation.StartCapture => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int Capacity
    {
        get => Model.Capacity;
        set { if (Equals(Model.Capacity, value)) return; Model.Capacity = value; NumericValueChanged(nameof(Capacity)); Changed(); }
    }
    public string CapacityText { get => GetNumericText(nameof(Capacity), Model.Capacity); set => SetIntegerText(nameof(Capacity), value, parsed => Capacity = parsed); }
    public bool IsCapacityActive => Model.ActiveFields.Contains(nameof(StartCaptureSetting.Capacity));
    public Visibility CapacityVisibility => IsCapacityActive ? Visibility.Visible : Visibility.Collapsed;
    public string CapacityLabel => Operation switch
    {
        StartCaptureOperation.StartCapture => "抓包缓冲容量",
        _ => ""
    };
    public string CapacityInfo => Operation switch
    {
        StartCaptureOperation.StartCapture => "SDK 缓冲/接收容量，不是已捕获的帧数；读取结果保留原始抓包格式 整数支持十进制和 0x 十六进制。 范围：1～16777216。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(StartCaptureSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        StartCaptureOperation.StartCapture => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        StartCaptureOperation.StartCapture => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(StartCaptureSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        StartCaptureOperation.StartCapture => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        StartCaptureOperation.StartCapture => "可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
