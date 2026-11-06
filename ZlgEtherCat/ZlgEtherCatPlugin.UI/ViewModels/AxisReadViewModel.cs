using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class AxisReadViewModel : ZlgViewModelBase<AxisReadSetting>
{
    public AxisReadOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<AxisReadOperation, string>> OperationChoices => [new(AxisReadOperation.ReadAxisState, "读取轴状态"), new(AxisReadOperation.ReadAxisMode, "读取实际模式"), new(AxisReadOperation.ReadAxisFeedback, "读取综合反馈"), new(AxisReadOperation.ReadAxisDiagnostics, "读取轴报警诊断")];
    public string OperationTitle => Operation switch
    {
        AxisReadOperation.ReadAxisState => "读取轴状态",
        AxisReadOperation.ReadAxisMode => "读取实际模式",
        AxisReadOperation.ReadAxisFeedback => "读取综合反馈",
        AxisReadOperation.ReadAxisDiagnostics => "读取轴报警诊断",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        AxisReadOperation.ReadAxisState => "目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。\n前置步骤：绑定轴（ZlgEtherCat.BindAxis）。\n独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。\n返回：MotionDriveState：Unknown、NotReady、SwitchOnDisabled、ReadyToSwitchOn、SwitchedOn、OperationEnabled、QuickStopActive、FaultReactionActive、Fault。",
        AxisReadOperation.ReadAxisMode => "目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。\n前置步骤：绑定轴（ZlgEtherCat.BindAxis）。\n独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。\n返回：sbyte：驱动实际模式（1=PP、3=PV、4=PT、6=HM、8=CSP、9=CSV、10=CST）。",
        AxisReadOperation.ReadAxisFeedback => "目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。\n前置步骤：绑定轴（ZlgEtherCat.BindAxis）。\n有 PDO 映射时读取一张完整快照；无 PDO 映射时按顺序读取 SDO，Sequence=0，数据不保证原子一致。 PDO 时间来自对应快照的主机读取完成时刻；SDO 时间在多次对象读取完成后记录，不保证各字段来自同一采样时刻。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。\n返回：对象：StatusWord（ushort）、Mode（sbyte?）、Position（int?）、Velocity（int?）、Torque（short?）、Sequence（long）、ReceivedTimestamp（单调时钟刻度）、TimestampSource=\"HostMonotonicReadCompletion\"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；未映射反馈可为 null，SDO 多次读取后的主机时刻不表示原子采样。",
        AxisReadOperation.ReadAxisDiagnostics => "目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。\n前置步骤：绑定轴（ZlgEtherCat.BindAxis）。\n独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 报警对象 0x603F/0x1001 需要 SDO，须在 Queue 周期输出停止后读取。\n返回：对象：StatusWord（ushort）、State（MotionDriveState）、Mode（sbyte）、ErrorCode（ushort，0x603F）、ErrorRegister（byte，0x1001）。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(AxisReadSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        AxisReadOperation.ReadAxisState => "连接名",
        AxisReadOperation.ReadAxisMode => "连接名",
        AxisReadOperation.ReadAxisFeedback => "连接名",
        AxisReadOperation.ReadAxisDiagnostics => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        AxisReadOperation.ReadAxisState => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        AxisReadOperation.ReadAxisMode => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        AxisReadOperation.ReadAxisFeedback => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        AxisReadOperation.ReadAxisDiagnostics => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string AxisName
    {
        get => Model.AxisName;
        set { if (Equals(Model.AxisName, value)) return; Model.AxisName = value; Changed(); }
    }
    public bool IsAxisNameActive => Model.ActiveFields.Contains(nameof(AxisReadSetting.AxisName));
    public Visibility AxisNameVisibility => IsAxisNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string AxisNameLabel => Operation switch
    {
        AxisReadOperation.ReadAxisState => "轴名称",
        AxisReadOperation.ReadAxisMode => "轴名称",
        AxisReadOperation.ReadAxisFeedback => "轴名称",
        AxisReadOperation.ReadAxisDiagnostics => "轴名称",
        _ => ""
    };
    public string AxisNameInfo => Operation switch
    {
        AxisReadOperation.ReadAxisState => "本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号",
        AxisReadOperation.ReadAxisMode => "本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号",
        AxisReadOperation.ReadAxisFeedback => "本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号",
        AxisReadOperation.ReadAxisDiagnostics => "本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(AxisReadSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        AxisReadOperation.ReadAxisState => "步骤总超时（ms）",
        AxisReadOperation.ReadAxisMode => "步骤总超时（ms）",
        AxisReadOperation.ReadAxisFeedback => "步骤总超时（ms）",
        AxisReadOperation.ReadAxisDiagnostics => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        AxisReadOperation.ReadAxisState => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        AxisReadOperation.ReadAxisMode => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        AxisReadOperation.ReadAxisFeedback => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        AxisReadOperation.ReadAxisDiagnostics => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(AxisReadSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        AxisReadOperation.ReadAxisState => "结果变量",
        AxisReadOperation.ReadAxisMode => "结果变量",
        AxisReadOperation.ReadAxisFeedback => "结果变量",
        AxisReadOperation.ReadAxisDiagnostics => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        AxisReadOperation.ReadAxisState => "可选已声明变量完整路径；MotionDriveState：Unknown、NotReady、SwitchOnDisabled、ReadyToSwitchOn、SwitchedOn、OperationEnabled、QuickStopActive、FaultReactionActive、Fault。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        AxisReadOperation.ReadAxisMode => "可选已声明变量完整路径；sbyte：驱动实际模式（1=PP、3=PV、4=PT、6=HM、8=CSP、9=CSV、10=CST）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        AxisReadOperation.ReadAxisFeedback => "可选已声明变量完整路径；对象：StatusWord（ushort）、Mode（sbyte?）、Position（int?）、Velocity（int?）、Torque（short?）、Sequence（long）、ReceivedTimestamp（单调时钟刻度）、TimestampSource=\"HostMonotonicReadCompletion\"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；未映射反馈可为 null，SDO 多次读取后的主机时刻不表示原子采样。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        AxisReadOperation.ReadAxisDiagnostics => "可选已声明变量完整路径；对象：StatusWord（ushort）、State（MotionDriveState）、Mode（sbyte）、ErrorCode（ushort，0x603F）、ErrorRegister（byte，0x1001）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
