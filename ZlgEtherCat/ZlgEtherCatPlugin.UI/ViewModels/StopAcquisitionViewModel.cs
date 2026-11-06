using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class StopAcquisitionViewModel : ZlgViewModelBase<StopAcquisitionSetting>
{
    public StopAcquisitionOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<StopAcquisitionOperation, string>> OperationChoices => [new(StopAcquisitionOperation.StopAcquisition, "停止 PDO 后台采集")];
    public string OperationTitle => Operation switch
    {
        StopAcquisitionOperation.StopAcquisition => "停止 PDO 后台采集",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        StopAcquisitionOperation.StopAcquisition => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：启动 PDO 后台采集（ZlgEtherCat.StartAcquisition）。\n等待本通道采集任务真实退出；未启动报错；仍有依赖读取池或运动时拒绝直接停止。 必须先关闭该连接两类所有命名读取池。 停止后恢复共享 SDK 线程原优先级；保留 RX 诊断证据并标 Monitoring=false，不把停止采集当作队列异常已经恢复。\n返回：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(StopAcquisitionSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        StopAcquisitionOperation.StopAcquisition => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        StopAcquisitionOperation.StopAcquisition => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(StopAcquisitionSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        StopAcquisitionOperation.StopAcquisition => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        StopAcquisitionOperation.StopAcquisition => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(StopAcquisitionSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        StopAcquisitionOperation.StopAcquisition => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        StopAcquisitionOperation.StopAcquisition => "可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
