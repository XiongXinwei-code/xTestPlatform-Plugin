using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ReadHistoryViewModel : ZlgViewModelBase<ReadHistorySetting>
{
    public ReadHistoryOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ReadHistoryOperation, string>> OperationChoices => [new(ReadHistoryOperation.ReadHistory, "读取历史采样")];
    public string OperationTitle => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "读取历史采样",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：启动 PDO 后台采集（ZlgEtherCat.StartAcquisition）。\n读取有界缓冲内样本；报告无法提供的历史范围，不伪造丢帧数。 Dropped 仅统计本地历史缓冲淘汰，SkippedSamples 仅统计读取项跳过的软件序号；二者都不是硬件丢帧计数。\n返回：对象：Dropped、DroppedMeaning=\"LocalHistoryEvictions\"、HardwareDroppedFrames=null、OldestSequence、Samples；Dropped 仅为本地历史缓冲淘汰数。每个样本有 Sequence、ReceivedTimestamp、Data（完整主站输入 byte[]）、QueueBacklog、LocallyValid、HasQueueEvent，以及 TimestampSource=\"HostMonotonicReadCompletion\"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；ReceivedTimestamp 是主机读取完成的单调时钟刻度，不是设备采样时刻，LocalAgeMs 不包含此前 RX 排队时间。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ReadHistorySetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int AfterSequence
    {
        get => Model.AfterSequence;
        set { if (Equals(Model.AfterSequence, value)) return; Model.AfterSequence = value; NumericValueChanged(nameof(AfterSequence)); Changed(); }
    }
    public string AfterSequenceText { get => GetNumericText(nameof(AfterSequence), Model.AfterSequence); set => SetIntegerText(nameof(AfterSequence), value, parsed => AfterSequence = parsed); }
    public bool IsAfterSequenceActive => Model.ActiveFields.Contains(nameof(ReadHistorySetting.AfterSequence));
    public Visibility AfterSequenceVisibility => IsAfterSequenceActive ? Visibility.Visible : Visibility.Collapsed;
    public string AfterSequenceLabel => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "起始软件序号",
        _ => ""
    };
    public string AfterSequenceInfo => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "long 软件样本序号的兼容常量输入（此字段保留 int 范围）；仅返回序号大于它的样本，AfterSequenceExpression 非空时优先；不使用设备周期号 整数支持十进制和 0x 十六进制。 范围：0～2147483647。",
        _ => ""
    };

    public string AfterSequenceExpression
    {
        get => Model.AfterSequenceExpression;
        set { if (Equals(Model.AfterSequenceExpression, value)) return; Model.AfterSequenceExpression = value; Changed(); }
    }
    public bool IsAfterSequenceExpressionActive => Model.ActiveFields.Contains(nameof(ReadHistorySetting.AfterSequenceExpression));
    public Visibility AfterSequenceExpressionVisibility => IsAfterSequenceExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string AfterSequenceExpressionLabel => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "起始软件序号表达式（可选）",
        _ => ""
    };
    public string AfterSequenceExpressionInfo => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "留空使用上方常量；非空时读取 long 软件样本序号，例如上一张样本的 Sequence，结果必须大于等于 0。",
        _ => ""
    };

    public int MaxSamples
    {
        get => Model.MaxSamples;
        set { if (Equals(Model.MaxSamples, value)) return; Model.MaxSamples = value; NumericValueChanged(nameof(MaxSamples)); Changed(); }
    }
    public string MaxSamplesText { get => GetNumericText(nameof(MaxSamples), Model.MaxSamples); set => SetIntegerText(nameof(MaxSamples), value, parsed => MaxSamples = parsed); }
    public bool IsMaxSamplesActive => Model.ActiveFields.Contains(nameof(ReadHistorySetting.MaxSamples));
    public Visibility MaxSamplesVisibility => IsMaxSamplesActive ? Visibility.Visible : Visibility.Collapsed;
    public string MaxSamplesLabel => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "最多样本数",
        _ => ""
    };
    public string MaxSamplesInfo => Operation switch
    {
        ReadHistoryOperation.ReadHistory => " 整数支持十进制和 0x 十六进制。 范围：1～10000。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ReadHistorySetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ReadHistorySetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ReadHistoryOperation.ReadHistory => "可选已声明变量完整路径；对象：Dropped、DroppedMeaning=\"LocalHistoryEvictions\"、HardwareDroppedFrames=null、OldestSequence、Samples；Dropped 仅为本地历史缓冲淘汰数。每个样本有 Sequence、ReceivedTimestamp、Data（完整主站输入 byte[]）、QueueBacklog、LocallyValid、HasQueueEvent，以及 TimestampSource=\"HostMonotonicReadCompletion\"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；ReceivedTimestamp 是主机读取完成的单调时钟刻度，不是设备采样时刻，LocalAgeMs 不包含此前 RX 排队时间。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
