using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class CyclicTargetViewModel : ZlgViewModelBase<CyclicTargetSetting>
{
    public CyclicTargetOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<CyclicTargetOperation, string>> OperationChoices => [new(CyclicTargetOperation.WriteCspTarget, "写入 CSP 位置目标"), new(CyclicTargetOperation.WriteCsvTarget, "写入 CSV 速度目标"), new(CyclicTargetOperation.WriteCstTarget, "写入 CST 转矩目标")];
    public string OperationTitle => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "写入 CSP 位置目标",
        CyclicTargetOperation.WriteCsvTarget => "写入 CSV 速度目标",
        CyclicTargetOperation.WriteCstTarget => "写入 CST 转矩目标",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。\n前置步骤：绑定轴（ZlgEtherCat.BindAxis）。\n独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 仅提交单次目标，不等待运动完成；必须配置对应模式的 Queue PDO 并维护目标更新节奏。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 要求轴已运行使能，且实际模式为 8 / CSP；该模式必须包含在 AllowedModes 白名单中。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。",
        CyclicTargetOperation.WriteCsvTarget => "目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。\n前置步骤：绑定轴（ZlgEtherCat.BindAxis）。\n独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 仅提交单次目标，不等待运动完成；必须配置对应模式的 Queue PDO 并维护目标更新节奏。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 要求轴已运行使能，且实际模式为 9 / CSV；该模式必须包含在 AllowedModes 白名单中。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。",
        CyclicTargetOperation.WriteCstTarget => "目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。\n前置步骤：绑定轴（ZlgEtherCat.BindAxis）。\n独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 仅提交单次目标，不等待运动完成；必须配置对应模式的 Queue PDO 并维护目标更新节奏。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 要求轴已运行使能，且实际模式为 10 / CST；该模式必须包含在 AllowedModes 白名单中。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(CyclicTargetSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "连接名",
        CyclicTargetOperation.WriteCsvTarget => "连接名",
        CyclicTargetOperation.WriteCstTarget => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        CyclicTargetOperation.WriteCsvTarget => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        CyclicTargetOperation.WriteCstTarget => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string AxisName
    {
        get => Model.AxisName;
        set { if (Equals(Model.AxisName, value)) return; Model.AxisName = value; Changed(); }
    }
    public bool IsAxisNameActive => Model.ActiveFields.Contains(nameof(CyclicTargetSetting.AxisName));
    public Visibility AxisNameVisibility => IsAxisNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string AxisNameLabel => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "轴名称",
        CyclicTargetOperation.WriteCsvTarget => "轴名称",
        CyclicTargetOperation.WriteCstTarget => "轴名称",
        _ => ""
    };
    public string AxisNameInfo => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号",
        CyclicTargetOperation.WriteCsvTarget => "本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号",
        CyclicTargetOperation.WriteCstTarget => "本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号",
        _ => ""
    };

    public string Position
    {
        get => Model.Position;
        set { if (Equals(Model.Position, value)) return; Model.Position = value; Changed(); }
    }
    public bool IsPositionActive => Model.ActiveFields.Contains(nameof(CyclicTargetSetting.Position));
    public Visibility PositionVisibility => IsPositionActive ? Visibility.Visible : Visibility.Collapsed;
    public string PositionLabel => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "位置目标",
        _ => ""
    };
    public string PositionInfo => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "求值结果为 int；驱动原始单位",
        _ => ""
    };

    public string Velocity
    {
        get => Model.Velocity;
        set { if (Equals(Model.Velocity, value)) return; Model.Velocity = value; Changed(); }
    }
    public bool IsVelocityActive => Model.ActiveFields.Contains(nameof(CyclicTargetSetting.Velocity));
    public Visibility VelocityVisibility => IsVelocityActive ? Visibility.Visible : Visibility.Collapsed;
    public string VelocityLabel => Operation switch
    {
        CyclicTargetOperation.WriteCsvTarget => "速度目标",
        _ => ""
    };
    public string VelocityInfo => Operation switch
    {
        CyclicTargetOperation.WriteCsvTarget => "求值结果为 int；驱动原始单位",
        _ => ""
    };

    public string Torque
    {
        get => Model.Torque;
        set { if (Equals(Model.Torque, value)) return; Model.Torque = value; Changed(); }
    }
    public bool IsTorqueActive => Model.ActiveFields.Contains(nameof(CyclicTargetSetting.Torque));
    public Visibility TorqueVisibility => IsTorqueActive ? Visibility.Visible : Visibility.Collapsed;
    public string TorqueLabel => Operation switch
    {
        CyclicTargetOperation.WriteCstTarget => "转矩目标",
        _ => ""
    };
    public string TorqueInfo => Operation switch
    {
        CyclicTargetOperation.WriteCstTarget => "求值结果为 short；驱动原始单位",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(CyclicTargetSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "步骤总超时（ms）",
        CyclicTargetOperation.WriteCsvTarget => "步骤总超时（ms）",
        CyclicTargetOperation.WriteCstTarget => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        CyclicTargetOperation.WriteCsvTarget => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        CyclicTargetOperation.WriteCstTarget => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(CyclicTargetSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "结果变量",
        CyclicTargetOperation.WriteCsvTarget => "结果变量",
        CyclicTargetOperation.WriteCstTarget => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        CyclicTargetOperation.WriteCspTarget => "可选已声明变量完整路径；MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        CyclicTargetOperation.WriteCsvTarget => "可选已声明变量完整路径；MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        CyclicTargetOperation.WriteCstTarget => "可选已声明变量完整路径；MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
