using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class MovePositionViewModel : ZlgViewModelBase<MovePositionSetting>
{
    public MovePositionOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<MovePositionOperation, string>> OperationChoices => [new(MovePositionOperation.MovePosition, "位置运动")];
    public string OperationTitle => Operation switch
    {
        MovePositionOperation.MovePosition => "位置运动",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        MovePositionOperation.MovePosition => "目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。\n前置步骤：绑定轴（ZlgEtherCat.BindAxis）。\n独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 本步骤等待本次目标握手及驱动目标到达；轮廓速度、加速度和减速度必须大于 0。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 要求轴已运行使能，且实际模式为 1 / PP；该模式必须包含在 AllowedModes 白名单中。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(MovePositionSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        MovePositionOperation.MovePosition => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        MovePositionOperation.MovePosition => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string AxisName
    {
        get => Model.AxisName;
        set { if (Equals(Model.AxisName, value)) return; Model.AxisName = value; Changed(); }
    }
    public bool IsAxisNameActive => Model.ActiveFields.Contains(nameof(MovePositionSetting.AxisName));
    public Visibility AxisNameVisibility => IsAxisNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string AxisNameLabel => Operation switch
    {
        MovePositionOperation.MovePosition => "轴名称",
        _ => ""
    };
    public string AxisNameInfo => Operation switch
    {
        MovePositionOperation.MovePosition => "本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号",
        _ => ""
    };

    public string Position
    {
        get => Model.Position;
        set { if (Equals(Model.Position, value)) return; Model.Position = value; Changed(); }
    }
    public bool IsPositionActive => Model.ActiveFields.Contains(nameof(MovePositionSetting.Position));
    public Visibility PositionVisibility => IsPositionActive ? Visibility.Visible : Visibility.Collapsed;
    public string PositionLabel => Operation switch
    {
        MovePositionOperation.MovePosition => "目标位置",
        _ => ""
    };
    public string PositionInfo => Operation switch
    {
        MovePositionOperation.MovePosition => "求值结果为 int；驱动原始单位",
        _ => ""
    };

    public bool Relative
    {
        get => Model.Relative;
        set { if (Equals(Model.Relative, value)) return; Model.Relative = value; Changed(); }
    }
    public bool IsRelativeActive => Model.ActiveFields.Contains(nameof(MovePositionSetting.Relative));
    public Visibility RelativeVisibility => IsRelativeActive ? Visibility.Visible : Visibility.Collapsed;
    public string RelativeLabel => Operation switch
    {
        MovePositionOperation.MovePosition => "相对运动",
        _ => ""
    };
    public string RelativeInfo => Operation switch
    {
        MovePositionOperation.MovePosition => "",
        _ => ""
    };

    public string ProfileVelocity
    {
        get => Model.ProfileVelocity;
        set { if (Equals(Model.ProfileVelocity, value)) return; Model.ProfileVelocity = value; Changed(); }
    }
    public bool IsProfileVelocityActive => Model.ActiveFields.Contains(nameof(MovePositionSetting.ProfileVelocity));
    public Visibility ProfileVelocityVisibility => IsProfileVelocityActive ? Visibility.Visible : Visibility.Collapsed;
    public string ProfileVelocityLabel => Operation switch
    {
        MovePositionOperation.MovePosition => "轮廓速度",
        _ => ""
    };
    public string ProfileVelocityInfo => Operation switch
    {
        MovePositionOperation.MovePosition => "求值为 uint，必须大于 0；默认 0 是待配置占位，不能执行。驱动原始单位，请按驱动量纲配置，不能默认当作 mm/s 或 rpm",
        _ => ""
    };

    public string Acceleration
    {
        get => Model.Acceleration;
        set { if (Equals(Model.Acceleration, value)) return; Model.Acceleration = value; Changed(); }
    }
    public bool IsAccelerationActive => Model.ActiveFields.Contains(nameof(MovePositionSetting.Acceleration));
    public Visibility AccelerationVisibility => IsAccelerationActive ? Visibility.Visible : Visibility.Collapsed;
    public string AccelerationLabel => Operation switch
    {
        MovePositionOperation.MovePosition => "加速度",
        _ => ""
    };
    public string AccelerationInfo => Operation switch
    {
        MovePositionOperation.MovePosition => "求值为 uint，必须大于 0；默认 0 是待配置占位，不能执行。驱动原始单位，请按驱动量纲配置，不能默认当作 mm/s 或 rpm",
        _ => ""
    };

    public string Deceleration
    {
        get => Model.Deceleration;
        set { if (Equals(Model.Deceleration, value)) return; Model.Deceleration = value; Changed(); }
    }
    public bool IsDecelerationActive => Model.ActiveFields.Contains(nameof(MovePositionSetting.Deceleration));
    public Visibility DecelerationVisibility => IsDecelerationActive ? Visibility.Visible : Visibility.Collapsed;
    public string DecelerationLabel => Operation switch
    {
        MovePositionOperation.MovePosition => "减速度",
        _ => ""
    };
    public string DecelerationInfo => Operation switch
    {
        MovePositionOperation.MovePosition => "求值为 uint，必须大于 0；默认 0 是待配置占位，不能执行。驱动原始单位，请按驱动量纲配置，不能默认当作 mm/s 或 rpm",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(MovePositionSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        MovePositionOperation.MovePosition => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        MovePositionOperation.MovePosition => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(MovePositionSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        MovePositionOperation.MovePosition => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        MovePositionOperation.MovePosition => "可选已声明变量完整路径；MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
