using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class RunTorqueViewModel : ZlgViewModelBase<RunTorqueSetting>
{
    public RunTorqueOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<RunTorqueOperation, string>> OperationChoices => [new(RunTorqueOperation.RunTorque, "转矩控制")];
    public string OperationTitle => Operation switch
    {
        RunTorqueOperation.RunTorque => "转矩控制",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        RunTorqueOperation.RunTorque => "目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。\n前置步骤：绑定轴（ZlgEtherCat.BindAxis）。\n独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 目标提交后返回，持续运动必须显式执行 StopMotion；不等待速度/转矩归零。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 要求轴已运行使能，且实际模式为 4 / PT；该模式必须包含在 AllowedModes 白名单中。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(RunTorqueSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        RunTorqueOperation.RunTorque => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        RunTorqueOperation.RunTorque => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string AxisName
    {
        get => Model.AxisName;
        set { if (Equals(Model.AxisName, value)) return; Model.AxisName = value; Changed(); }
    }
    public bool IsAxisNameActive => Model.ActiveFields.Contains(nameof(RunTorqueSetting.AxisName));
    public Visibility AxisNameVisibility => IsAxisNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string AxisNameLabel => Operation switch
    {
        RunTorqueOperation.RunTorque => "轴名称",
        _ => ""
    };
    public string AxisNameInfo => Operation switch
    {
        RunTorqueOperation.RunTorque => "本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号",
        _ => ""
    };

    public string Torque
    {
        get => Model.Torque;
        set { if (Equals(Model.Torque, value)) return; Model.Torque = value; Changed(); }
    }
    public bool IsTorqueActive => Model.ActiveFields.Contains(nameof(RunTorqueSetting.Torque));
    public Visibility TorqueVisibility => IsTorqueActive ? Visibility.Visible : Visibility.Collapsed;
    public string TorqueLabel => Operation switch
    {
        RunTorqueOperation.RunTorque => "目标转矩",
        _ => ""
    };
    public string TorqueInfo => Operation switch
    {
        RunTorqueOperation.RunTorque => "求值结果为 short；驱动原始单位",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(RunTorqueSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        RunTorqueOperation.RunTorque => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        RunTorqueOperation.RunTorque => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(RunTorqueSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        RunTorqueOperation.RunTorque => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        RunTorqueOperation.RunTorque => "可选已声明变量完整路径；MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
