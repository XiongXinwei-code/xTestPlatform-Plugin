using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class ReadPiRangeViewModel : ZlgViewModelBase<ReadPiRangeSetting>
{
    public ReadPiRangeOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<ReadPiRangeOperation, string>> OperationChoices => [new(ReadPiRangeOperation.ReadPiRange, "读取从站 PDO 范围")];
    public string OperationTitle => Operation switch
    {
        ReadPiRangeOperation.ReadPiRange => "读取从站 PDO 范围",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        ReadPiRangeOperation.ReadPiRange => "目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。\n前置步骤：加载 ENI 并启动主站（ZlgEtherCat.StartBus）。\n\n返回：对象：SlaveIndex、Input、Output、HasMetadata、InputMetadataAvailable、OutputMetadataAvailable、InputFields、OutputFields、ObjectIdentityVerified=false。HasMetadata 仅表示该从站至少有一个 ENI 字段，两个方向标志仅表示该方向有字段，均不保证完整覆盖。Input/Output 的 Offset/Length 是全局字节起点/长度；字段含 Name、EniDataType、AbsoluteBitOffset、RelativeBitOffset、BitLength、ByteAligned、ScalarType、ByteOrder。字段名不证明对象索引或物理身份。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(ReadPiRangeSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        ReadPiRangeOperation.ReadPiRange => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        ReadPiRangeOperation.ReadPiRange => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public int SlaveIndex
    {
        get => Model.SlaveIndex;
        set { if (Equals(Model.SlaveIndex, value)) return; Model.SlaveIndex = value; NumericValueChanged(nameof(SlaveIndex)); Changed(); }
    }
    public string SlaveIndexText { get => GetNumericText(nameof(SlaveIndex), Model.SlaveIndex); set => SetIntegerText(nameof(SlaveIndex), value, parsed => SlaveIndex = parsed); }
    public bool IsSlaveIndexActive => Model.ActiveFields.Contains(nameof(ReadPiRangeSetting.SlaveIndex));
    public Visibility SlaveIndexVisibility => IsSlaveIndexActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexLabel => Operation switch
    {
        ReadPiRangeOperation.ReadPiRange => "从站 ENI 索引（常量）",
        _ => ""
    };
    public string SlaveIndexInfo => Operation switch
    {
        ReadPiRangeOperation.ReadPiRange => "本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。 整数支持十进制和 0x 十六进制。 范围：0～65534。",
        _ => ""
    };

    public string SlaveIndexExpression
    {
        get => Model.SlaveIndexExpression;
        set { if (Equals(Model.SlaveIndexExpression, value)) return; Model.SlaveIndexExpression = value; Changed(); }
    }
    public bool IsSlaveIndexExpressionActive => Model.ActiveFields.Contains(nameof(ReadPiRangeSetting.SlaveIndexExpression));
    public Visibility SlaveIndexExpressionVisibility => IsSlaveIndexExpressionActive ? Visibility.Visible : Visibility.Collapsed;
    public string SlaveIndexExpressionLabel => Operation switch
    {
        ReadPiRangeOperation.ReadPiRange => "从站 ENI 索引表达式（可选）",
        _ => ""
    };
    public string SlaveIndexExpressionInfo => Operation switch
    {
        ReadPiRangeOperation.ReadPiRange => "留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。 范围：0～65534。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(ReadPiRangeSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        ReadPiRangeOperation.ReadPiRange => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        ReadPiRangeOperation.ReadPiRange => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(ReadPiRangeSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        ReadPiRangeOperation.ReadPiRange => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        ReadPiRangeOperation.ReadPiRange => "可选已声明变量完整路径；对象：SlaveIndex、Input、Output、HasMetadata、InputMetadataAvailable、OutputMetadataAvailable、InputFields、OutputFields、ObjectIdentityVerified=false。HasMetadata 仅表示该从站至少有一个 ENI 字段，两个方向标志仅表示该方向有字段，均不保证完整覆盖。Input/Output 的 Offset/Length 是全局字节起点/长度；字段含 Name、EniDataType、AbsoluteBitOffset、RelativeBitOffset、BitLength、ByteAligned、ScalarType、ByteOrder。字段名不证明对象索引或物理身份。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
