using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class BoardUpdateViewModel : ZlgViewModelBase<BoardUpdateSetting>
{
    public BoardUpdateOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<BoardUpdateOperation, string>> OperationChoices => [new(BoardUpdateOperation.BoardUpdate, "更新板卡文件")];
    public string OperationTitle => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => "更新板卡文件",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n仅停机维护使用，依据厂商文件格式；不自动复位或重试。 本操作要求整个连接没有任何绑定轴；即使目标是其他从站，也须先停轴、去使能并解绑。 要求连接内无绑定轴、两类命名读取池均已关闭、采集和周期输出已停止、过程映像交换已禁用。 主站总线必须已停止。 整块板卡需由本通道独占维护；其他打开通道会阻止本操作。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(BoardUpdateSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string LocalPath
    {
        get => Model.LocalPath;
        set { if (Equals(Model.LocalPath, value)) return; Model.LocalPath = value; Changed(); }
    }
    public bool IsLocalPathActive => Model.ActiveFields.Contains(nameof(BoardUpdateSetting.LocalPath));
    public Visibility LocalPathVisibility => IsLocalPathActive ? Visibility.Visible : Visibility.Collapsed;
    public string LocalPathLabel => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => "本地文件路径",
        _ => ""
    };
    public string LocalPathInfo => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => "本地已有源文件路径表达式；本次把此文件内容写入或更新到设备，不是本地保存目标，也不是设备端路径。",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(BoardUpdateSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(BoardUpdateSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        BoardUpdateOperation.BoardUpdate => "可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
