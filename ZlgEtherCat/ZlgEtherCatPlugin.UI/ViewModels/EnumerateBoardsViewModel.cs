using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class EnumerateBoardsViewModel : ZlgViewModelBase<EnumerateBoardsSetting>
{
    public EnumerateBoardsOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<EnumerateBoardsOperation, string>> OperationChoices => [new(EnumerateBoardsOperation.EnumerateBoards, "枚举板卡")];
    public string OperationTitle => Operation switch
    {
        EnumerateBoardsOperation.EnumerateBoards => "枚举板卡",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        EnumerateBoardsOperation.EnumerateBoards => "驱动级只读枚举，不需要 ConnectionName，不打开通道。\n前置步骤：无。\n读取驱动/API及板卡通道信息，不打开主站。\n返回：板卡数组：每项 Index（驱动枚举索引）与 Info（Name、Version、SerialNumber、Alias、ChannelCount、FpgaVersion）；同型号用序列号区分。",
        _ => "当前功能未定义，不能执行。"
    };
    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(EnumerateBoardsSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        EnumerateBoardsOperation.EnumerateBoards => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        EnumerateBoardsOperation.EnumerateBoards => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(EnumerateBoardsSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        EnumerateBoardsOperation.EnumerateBoards => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        EnumerateBoardsOperation.EnumerateBoards => "可选已声明变量完整路径；板卡数组：每项 Index（驱动枚举索引）与 Info（Name、Version、SerialNumber、Alias、ChannelCount、FpgaVersion）；同型号用序列号区分。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
