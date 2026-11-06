using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class StopMotionViewModel : ZlgViewModelBase<StopMotionSetting>
{
    public StopMotionOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<StopMotionOperation, string>> OperationChoices => [new(StopMotionOperation.StopMotion, "停止运动")];
    public string OperationTitle => Operation switch
    {
        StopMotionOperation.StopMotion => "停止运动",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        StopMotionOperation.StopMotion => "目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。\n前置步骤：绑定轴（ZlgEtherCat.BindAxis）。\n请求 Halt 或 QuickStop 后确认反馈；CSP/CSV/CST 必须显式 QuickStop=true，驱动停止选项须正确配置。停止确认需两张不同 PDO 样本或两次 SDO 实际速度读取，仍不证明物理或功能安全停止。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。\n返回：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(StopMotionSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        StopMotionOperation.StopMotion => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        StopMotionOperation.StopMotion => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string AxisName
    {
        get => Model.AxisName;
        set { if (Equals(Model.AxisName, value)) return; Model.AxisName = value; Changed(); }
    }
    public bool IsAxisNameActive => Model.ActiveFields.Contains(nameof(StopMotionSetting.AxisName));
    public Visibility AxisNameVisibility => IsAxisNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string AxisNameLabel => Operation switch
    {
        StopMotionOperation.StopMotion => "轴名称",
        _ => ""
    };
    public string AxisNameInfo => Operation switch
    {
        StopMotionOperation.StopMotion => "本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号",
        _ => ""
    };

    public bool QuickStop
    {
        get => Model.QuickStop;
        set { if (Equals(Model.QuickStop, value)) return; Model.QuickStop = value; Changed(); }
    }
    public bool IsQuickStopActive => Model.ActiveFields.Contains(nameof(StopMotionSetting.QuickStop));
    public Visibility QuickStopVisibility => IsQuickStopActive ? Visibility.Visible : Visibility.Collapsed;
    public string QuickStopLabel => Operation switch
    {
        StopMotionOperation.StopMotion => "使用快速停止",
        _ => ""
    };
    public string QuickStopInfo => Operation switch
    {
        StopMotionOperation.StopMotion => "",
        _ => ""
    };

    public string VelocityTolerance
    {
        get => Model.VelocityTolerance;
        set { if (Equals(Model.VelocityTolerance, value)) return; Model.VelocityTolerance = value; Changed(); }
    }
    public bool IsVelocityToleranceActive => Model.ActiveFields.Contains(nameof(StopMotionSetting.VelocityTolerance));
    public Visibility VelocityToleranceVisibility => IsVelocityToleranceActive ? Visibility.Visible : Visibility.Collapsed;
    public string VelocityToleranceLabel => Operation switch
    {
        StopMotionOperation.StopMotion => "停稳速度容差",
        _ => ""
    };
    public string VelocityToleranceInfo => Operation switch
    {
        StopMotionOperation.StopMotion => "求值为 int，必须大于等于 0；停止确认时允许的实际速度绝对值，单位与驱动实际速度相同",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(StopMotionSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        StopMotionOperation.StopMotion => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        StopMotionOperation.StopMotion => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(StopMotionSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        StopMotionOperation.StopMotion => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        StopMotionOperation.StopMotion => "可选已声明变量完整路径；MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
