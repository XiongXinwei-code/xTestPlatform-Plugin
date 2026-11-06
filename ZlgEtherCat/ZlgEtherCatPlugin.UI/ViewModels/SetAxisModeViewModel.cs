using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class SetAxisModeViewModel : ZlgViewModelBase<SetAxisModeSetting>
{
    public SetAxisModeOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<SetAxisModeOperation, string>> OperationChoices => [new(SetAxisModeOperation.SetAxisMode, "设置运行模式")];
    public string OperationTitle => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "设置运行模式",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。\n前置步骤：绑定轴（ZlgEtherCat.BindAxis）。\n独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 模式必须在 AllowedModes 中；切换前须停轴、去使能，故障状态不可切换。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(SetAxisModeSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string AxisName
    {
        get => Model.AxisName;
        set { if (Equals(Model.AxisName, value)) return; Model.AxisName = value; Changed(); }
    }
    public bool IsAxisNameActive => Model.ActiveFields.Contains(nameof(SetAxisModeSetting.AxisName));
    public Visibility AxisNameVisibility => IsAxisNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string AxisNameLabel => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "轴名称",
        _ => ""
    };
    public string AxisNameInfo => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号",
        _ => ""
    };

    public int Mode
    {
        get => Model.Mode;
        set { if (Equals(Model.Mode, value)) return; Model.Mode = value; NumericValueChanged(nameof(Mode)); Changed(); }
    }
    public IReadOnlyList<KeyValuePair<int, string>> ModeChoices => [new(1, "1 — PP 轮廓位置"), new(3, "3 — PV 轮廓速度"), new(4, "4 — PT 轮廓转矩"), new(6, "6 — HM 回零"), new(8, "8 — CSP 周期同步位置"), new(9, "9 — CSV 周期同步速度"), new(10, "10 — CST 周期同步转矩")];
    public bool IsModeActive => Model.ActiveFields.Contains(nameof(SetAxisModeSetting.Mode));
    public Visibility ModeVisibility => IsModeActive ? Visibility.Visible : Visibility.Collapsed;
    public string ModeLabel => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "运行模式",
        _ => ""
    };
    public string ModeInfo => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "必须包含在该轴 AllowedModes 白名单中；先设置再读取确认。类型代码不是 PDO 数据类型。 范围：1～10。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(SetAxisModeSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(SetAxisModeSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        SetAxisModeOperation.SetAxisMode => "可选已声明变量完整路径；MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
