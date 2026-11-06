using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class BindAxisViewModel : ZlgViewModelBase<BindAxisSetting>
{
    public BindAxisOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<BindAxisOperation, string>> OperationChoices => [new(BindAxisOperation.BindAxis, "绑定轴")];
    public string OperationTitle => Operation switch
    {
        BindAxisOperation.BindAxis => "绑定轴",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        BindAxisOperation.BindAxis => "目标为 ConnectionName 内已绑定的 AxisName；BindAxis 的 SlaveIndex 选择本 ENI 中的唯一从站。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n独立实现 CiA402 轴控制，使用 ZLG CoE/PDO 通路；只操作已绑定的当前轴，不依赖其他设备插件。 PDO 反馈/控制只支持 Queue 交换，并要求采集及相应映射有效；Queue 周期输出运行时拒绝需要 SDO 的访问，不自动降级。驱动原始单位须按驱动手册换算，Windows 后台不保证硬实时周期。 同一连接内 AxisName 与 SlaveIndex 均须未被其他轴占用；当前实现一个从站只绑定一个轴。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string AxisName
    {
        get => Model.AxisName;
        set { if (Equals(Model.AxisName, value)) return; Model.AxisName = value; Changed(); }
    }
    public bool IsAxisNameActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.AxisName));
    public Visibility AxisNameVisibility => IsAxisNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string AxisNameLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "轴名称",
        _ => ""
    };
    public string AxisNameInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "本连接内唯一的逻辑轴会话名表达式；BindAxis 将它绑定到指定从站，其他运动步骤只引用该名，不是驱动对象索引或物理轴号",
        _ => ""
    };

    public string SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; Changed(); }
    }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "从站 ENI 索引表达式",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "求值为 int，本连接 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址或别名。范围 0～65534，先用 EnumerateSlaves 核对。 范围：0～65534。",
        _ => ""
    };

    public string AllowedModes
    {
        get => Model.AllowedModes;
        set { if (Equals(Model.AllowedModes, value)) return; Model.AllowedModes = value; Changed(); }
    }
    public bool IsAllowedModesActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.AllowedModes));
    public Visibility AllowedModesVisibility => IsAllowedModesActive ? Visibility.Visible : Visibility.Collapsed;
    public string AllowedModesLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "允许的驱动模式",
        _ => ""
    };
    public string AllowedModesInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "逗号分隔且不重复：1=PP、3=PV、4=PT、6=HM、8=CSP、9=CSV、10=CST。仅勾选/填写驱动确实支持的模式；不配置不会自动推断",
        _ => ""
    };

    public int MaxFeedbackAgeMs
    {
        get => Model.MaxFeedbackAgeMs;
        set { if (Equals(Model.MaxFeedbackAgeMs, value)) return; Model.MaxFeedbackAgeMs = value; NumericValueChanged(nameof(MaxFeedbackAgeMs)); Changed(); }
    }
    public string MaxFeedbackAgeMsText { get => GetNumericText(nameof(MaxFeedbackAgeMs), Model.MaxFeedbackAgeMs); set => SetIntegerText(nameof(MaxFeedbackAgeMs), value, parsed => MaxFeedbackAgeMs = parsed); }
    public bool IsMaxFeedbackAgeMsActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.MaxFeedbackAgeMs));
    public Visibility MaxFeedbackAgeMsVisibility => IsMaxFeedbackAgeMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string MaxFeedbackAgeMsLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "反馈最大本地接收年龄（ms）",
        _ => ""
    };
    public string MaxFeedbackAgeMsInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "从主机读取完成的单调时钟时刻起算；不含数据此前在 SDK RX 队列中的等待时间，不能据此证明设备真实采样年龄。 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public int PollIntervalMs
    {
        get => Model.PollIntervalMs;
        set { if (Equals(Model.PollIntervalMs, value)) return; Model.PollIntervalMs = value; NumericValueChanged(nameof(PollIntervalMs)); Changed(); }
    }
    public string PollIntervalMsText { get => GetNumericText(nameof(PollIntervalMs), Model.PollIntervalMs); set => SetIntegerText(nameof(PollIntervalMs), value, parsed => PollIntervalMs = parsed); }
    public bool IsPollIntervalMsActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.PollIntervalMs));
    public Visibility PollIntervalMsVisibility => IsPollIntervalMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string PollIntervalMsLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "轮询间隔（ms）",
        _ => ""
    };
    public string PollIntervalMsInfo => Operation switch
    {
        BindAxisOperation.BindAxis => " 整数支持十进制和 0x 十六进制。 范围：1～1000。",
        _ => ""
    };

    public int InputByteOffset
    {
        get => Model.InputByteOffset;
        set { if (Equals(Model.InputByteOffset, value)) return; Model.InputByteOffset = value; NumericValueChanged(nameof(InputByteOffset)); Changed(); }
    }
    public string InputByteOffsetText { get => GetNumericText(nameof(InputByteOffset), Model.InputByteOffset); set => SetIntegerText(nameof(InputByteOffset), value, parsed => InputByteOffset = parsed); }
    public bool IsInputByteOffsetActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.InputByteOffset));
    public Visibility InputByteOffsetVisibility => IsInputByteOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string InputByteOffsetLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "轴输入区全局字节起点",
        _ => ""
    };
    public string InputByteOffsetInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "整个主站输入映像内的绝对字节起点，按 ReadPiRange 返回值配置；后续对象偏移同为绝对值 整数支持十进制和 0x 十六进制。 范围：0～16777215。",
        _ => ""
    };

    public int OutputByteOffset
    {
        get => Model.OutputByteOffset;
        set { if (Equals(Model.OutputByteOffset, value)) return; Model.OutputByteOffset = value; NumericValueChanged(nameof(OutputByteOffset)); Changed(); }
    }
    public string OutputByteOffsetText { get => GetNumericText(nameof(OutputByteOffset), Model.OutputByteOffset); set => SetIntegerText(nameof(OutputByteOffset), value, parsed => OutputByteOffset = parsed); }
    public bool IsOutputByteOffsetActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.OutputByteOffset));
    public Visibility OutputByteOffsetVisibility => IsOutputByteOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string OutputByteOffsetLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "轴输出区全局字节起点",
        _ => ""
    };
    public string OutputByteOffsetInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "整个主站输出映像内的绝对字节起点，按 ReadPiRange 返回值配置；后续对象偏移同为绝对值 整数支持十进制和 0x 十六进制。 范围：0～16777215。",
        _ => ""
    };

    public int InputByteLength
    {
        get => Model.InputByteLength;
        set { if (Equals(Model.InputByteLength, value)) return; Model.InputByteLength = value; NumericValueChanged(nameof(InputByteLength)); Changed(); }
    }
    public string InputByteLengthText { get => GetNumericText(nameof(InputByteLength), Model.InputByteLength); set => SetIntegerText(nameof(InputByteLength), value, parsed => InputByteLength = parsed); }
    public bool IsInputByteLengthActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.InputByteLength));
    public Visibility InputByteLengthVisibility => IsInputByteLengthActive ? Visibility.Visible : Visibility.Collapsed;
    public string InputByteLengthLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "轴输入区字节长度",
        _ => ""
    };
    public string InputByteLengthInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "0 表示不配置该方向 PDO，所有对应对象偏移应为 -1；非 0 必须在指定从站映像范围内。CSP/CSV/CST 需要实际 PDO 映射 整数支持十进制和 0x 十六进制。 范围：0～16777215。",
        _ => ""
    };

    public int OutputByteLength
    {
        get => Model.OutputByteLength;
        set { if (Equals(Model.OutputByteLength, value)) return; Model.OutputByteLength = value; NumericValueChanged(nameof(OutputByteLength)); Changed(); }
    }
    public string OutputByteLengthText { get => GetNumericText(nameof(OutputByteLength), Model.OutputByteLength); set => SetIntegerText(nameof(OutputByteLength), value, parsed => OutputByteLength = parsed); }
    public bool IsOutputByteLengthActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.OutputByteLength));
    public Visibility OutputByteLengthVisibility => IsOutputByteLengthActive ? Visibility.Visible : Visibility.Collapsed;
    public string OutputByteLengthLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "轴输出区字节长度",
        _ => ""
    };
    public string OutputByteLengthInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "0 表示不配置该方向 PDO，所有对应对象偏移应为 -1；非 0 必须在指定从站映像范围内。CSP/CSV/CST 需要实际 PDO 映射 整数支持十进制和 0x 十六进制。 范围：0～16777215。",
        _ => ""
    };

    public int StatusWordOffset
    {
        get => Model.StatusWordOffset;
        set { if (Equals(Model.StatusWordOffset, value)) return; Model.StatusWordOffset = value; NumericValueChanged(nameof(StatusWordOffset)); Changed(); }
    }
    public string StatusWordOffsetText { get => GetNumericText(nameof(StatusWordOffset), Model.StatusWordOffset); set => SetIntegerText(nameof(StatusWordOffset), value, parsed => StatusWordOffset = parsed); }
    public bool IsStatusWordOffsetActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.StatusWordOffset));
    public Visibility StatusWordOffsetVisibility => IsStatusWordOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string StatusWordOffsetLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "状态字偏移（UInt16 / 2 字节）",
        _ => ""
    };
    public string StatusWordOffsetInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "0x6041:00 固定 UInt16，2 字节，小端；状态位图。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6041:00，仍需用户按驱动字典核对。 整数支持十进制和 0x 十六进制。 范围：-1～16777215。",
        _ => ""
    };

    public int ActualPositionOffset
    {
        get => Model.ActualPositionOffset;
        set { if (Equals(Model.ActualPositionOffset, value)) return; Model.ActualPositionOffset = value; NumericValueChanged(nameof(ActualPositionOffset)); Changed(); }
    }
    public string ActualPositionOffsetText { get => GetNumericText(nameof(ActualPositionOffset), Model.ActualPositionOffset); set => SetIntegerText(nameof(ActualPositionOffset), value, parsed => ActualPositionOffset = parsed); }
    public bool IsActualPositionOffsetActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.ActualPositionOffset));
    public Visibility ActualPositionOffsetVisibility => IsActualPositionOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string ActualPositionOffsetLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "实际位置偏移（Int32 / 4 字节）",
        _ => ""
    };
    public string ActualPositionOffsetInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "0x6064:00 固定 Int32，4 字节，小端；驱动位置原始单位。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6064:00，仍需用户按驱动字典核对。 整数支持十进制和 0x 十六进制。 范围：-1～16777215。",
        _ => ""
    };

    public int ActualVelocityOffset
    {
        get => Model.ActualVelocityOffset;
        set { if (Equals(Model.ActualVelocityOffset, value)) return; Model.ActualVelocityOffset = value; NumericValueChanged(nameof(ActualVelocityOffset)); Changed(); }
    }
    public string ActualVelocityOffsetText { get => GetNumericText(nameof(ActualVelocityOffset), Model.ActualVelocityOffset); set => SetIntegerText(nameof(ActualVelocityOffset), value, parsed => ActualVelocityOffset = parsed); }
    public bool IsActualVelocityOffsetActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.ActualVelocityOffset));
    public Visibility ActualVelocityOffsetVisibility => IsActualVelocityOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string ActualVelocityOffsetLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "实际速度偏移（Int32 / 4 字节）",
        _ => ""
    };
    public string ActualVelocityOffsetInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "0x606C:00 固定 Int32，4 字节，小端；驱动速度原始单位。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x606C:00，仍需用户按驱动字典核对。 整数支持十进制和 0x 十六进制。 范围：-1～16777215。",
        _ => ""
    };

    public int ActualTorqueOffset
    {
        get => Model.ActualTorqueOffset;
        set { if (Equals(Model.ActualTorqueOffset, value)) return; Model.ActualTorqueOffset = value; NumericValueChanged(nameof(ActualTorqueOffset)); Changed(); }
    }
    public string ActualTorqueOffsetText { get => GetNumericText(nameof(ActualTorqueOffset), Model.ActualTorqueOffset); set => SetIntegerText(nameof(ActualTorqueOffset), value, parsed => ActualTorqueOffset = parsed); }
    public bool IsActualTorqueOffsetActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.ActualTorqueOffset));
    public Visibility ActualTorqueOffsetVisibility => IsActualTorqueOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string ActualTorqueOffsetLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "实际转矩偏移（Int16 / 2 字节）",
        _ => ""
    };
    public string ActualTorqueOffsetInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "0x6077:00 固定 Int16，2 字节，小端；驱动转矩原始单位，按驱动手册换算。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6077:00，仍需用户按驱动字典核对。 整数支持十进制和 0x 十六进制。 范围：-1～16777215。",
        _ => ""
    };

    public int ModeDisplayOffset
    {
        get => Model.ModeDisplayOffset;
        set { if (Equals(Model.ModeDisplayOffset, value)) return; Model.ModeDisplayOffset = value; NumericValueChanged(nameof(ModeDisplayOffset)); Changed(); }
    }
    public string ModeDisplayOffsetText { get => GetNumericText(nameof(ModeDisplayOffset), Model.ModeDisplayOffset); set => SetIntegerText(nameof(ModeDisplayOffset), value, parsed => ModeDisplayOffset = parsed); }
    public bool IsModeDisplayOffsetActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.ModeDisplayOffset));
    public Visibility ModeDisplayOffsetVisibility => IsModeDisplayOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string ModeDisplayOffsetLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "实际模式偏移（Int8 / 1 字节）",
        _ => ""
    };
    public string ModeDisplayOffsetInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "0x6061:00 固定 Int8，1 字节，小端；CiA402 模式代码。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6061:00，仍需用户按驱动字典核对。 整数支持十进制和 0x 十六进制。 范围：-1～16777215。",
        _ => ""
    };

    public int ControlWordOffset
    {
        get => Model.ControlWordOffset;
        set { if (Equals(Model.ControlWordOffset, value)) return; Model.ControlWordOffset = value; NumericValueChanged(nameof(ControlWordOffset)); Changed(); }
    }
    public string ControlWordOffsetText { get => GetNumericText(nameof(ControlWordOffset), Model.ControlWordOffset); set => SetIntegerText(nameof(ControlWordOffset), value, parsed => ControlWordOffset = parsed); }
    public bool IsControlWordOffsetActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.ControlWordOffset));
    public Visibility ControlWordOffsetVisibility => IsControlWordOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string ControlWordOffsetLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "控制字偏移（UInt16 / 2 字节）",
        _ => ""
    };
    public string ControlWordOffsetInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "0x6040:00 固定 UInt16，2 字节，小端；控制位图。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6040:00，仍需用户按驱动字典核对。 整数支持十进制和 0x 十六进制。 范围：-1～16777215。",
        _ => ""
    };

    public int TargetPositionOffset
    {
        get => Model.TargetPositionOffset;
        set { if (Equals(Model.TargetPositionOffset, value)) return; Model.TargetPositionOffset = value; NumericValueChanged(nameof(TargetPositionOffset)); Changed(); }
    }
    public string TargetPositionOffsetText { get => GetNumericText(nameof(TargetPositionOffset), Model.TargetPositionOffset); set => SetIntegerText(nameof(TargetPositionOffset), value, parsed => TargetPositionOffset = parsed); }
    public bool IsTargetPositionOffsetActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.TargetPositionOffset));
    public Visibility TargetPositionOffsetVisibility => IsTargetPositionOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string TargetPositionOffsetLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "目标位置偏移（Int32 / 4 字节）",
        _ => ""
    };
    public string TargetPositionOffsetInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "0x607A:00 固定 Int32，4 字节，小端；驱动位置原始单位。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x607A:00，仍需用户按驱动字典核对。 整数支持十进制和 0x 十六进制。 范围：-1～16777215。",
        _ => ""
    };

    public int TargetVelocityOffset
    {
        get => Model.TargetVelocityOffset;
        set { if (Equals(Model.TargetVelocityOffset, value)) return; Model.TargetVelocityOffset = value; NumericValueChanged(nameof(TargetVelocityOffset)); Changed(); }
    }
    public string TargetVelocityOffsetText { get => GetNumericText(nameof(TargetVelocityOffset), Model.TargetVelocityOffset); set => SetIntegerText(nameof(TargetVelocityOffset), value, parsed => TargetVelocityOffset = parsed); }
    public bool IsTargetVelocityOffsetActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.TargetVelocityOffset));
    public Visibility TargetVelocityOffsetVisibility => IsTargetVelocityOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string TargetVelocityOffsetLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "目标速度偏移（Int32 / 4 字节）",
        _ => ""
    };
    public string TargetVelocityOffsetInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "0x60FF:00 固定 Int32，4 字节，小端；驱动速度原始单位。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x60FF:00，仍需用户按驱动字典核对。 整数支持十进制和 0x 十六进制。 范围：-1～16777215。",
        _ => ""
    };

    public int TargetTorqueOffset
    {
        get => Model.TargetTorqueOffset;
        set { if (Equals(Model.TargetTorqueOffset, value)) return; Model.TargetTorqueOffset = value; NumericValueChanged(nameof(TargetTorqueOffset)); Changed(); }
    }
    public string TargetTorqueOffsetText { get => GetNumericText(nameof(TargetTorqueOffset), Model.TargetTorqueOffset); set => SetIntegerText(nameof(TargetTorqueOffset), value, parsed => TargetTorqueOffset = parsed); }
    public bool IsTargetTorqueOffsetActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.TargetTorqueOffset));
    public Visibility TargetTorqueOffsetVisibility => IsTargetTorqueOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string TargetTorqueOffsetLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "目标转矩偏移（Int16 / 2 字节）",
        _ => ""
    };
    public string TargetTorqueOffsetInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "0x6071:00 固定 Int16，2 字节，小端；驱动转矩原始单位，按驱动手册换算。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6071:00，仍需用户按驱动字典核对。 整数支持十进制和 0x 十六进制。 范围：-1～16777215。",
        _ => ""
    };

    public int ModeCommandOffset
    {
        get => Model.ModeCommandOffset;
        set { if (Equals(Model.ModeCommandOffset, value)) return; Model.ModeCommandOffset = value; NumericValueChanged(nameof(ModeCommandOffset)); Changed(); }
    }
    public string ModeCommandOffsetText { get => GetNumericText(nameof(ModeCommandOffset), Model.ModeCommandOffset); set => SetIntegerText(nameof(ModeCommandOffset), value, parsed => ModeCommandOffset = parsed); }
    public bool IsModeCommandOffsetActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.ModeCommandOffset));
    public Visibility ModeCommandOffsetVisibility => IsModeCommandOffsetActive ? Visibility.Visible : Visibility.Collapsed;
    public string ModeCommandOffsetLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "设定模式偏移（Int8 / 1 字节）",
        _ => ""
    };
    public string ModeCommandOffsetInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "0x6060:00 固定 Int8，1 字节，小端；CiA402 模式代码。填写整个主站相应输入/输出区的绝对字节偏移；-1=未映射，必须在轴区域内且不与其他对象重叠。ENI 有元数据时核对类型/宽度，字段 Name 不能证明实际对象就是 0x6060:00，仍需用户按驱动字典核对。 整数支持十进制和 0x 十六进制。 范围：-1～16777215。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(BindAxisSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        BindAxisOperation.BindAxis => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        BindAxisOperation.BindAxis => "可选已声明变量完整路径；MotionCommandResult 对象：AxisName、Operation、Detail；提交/反馈确认语义见本步骤行为。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
