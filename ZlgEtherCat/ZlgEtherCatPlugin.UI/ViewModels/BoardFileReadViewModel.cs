using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class BoardFileReadViewModel : ZlgViewModelBase<BoardFileReadSetting>
{
    public BoardFileReadOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<BoardFileReadOperation, string>> OperationChoices => [new(BoardFileReadOperation.BoardFileRead, "读取板卡文件")];
    public string OperationTitle => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "读取板卡文件",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：打开板卡通道（ZlgEtherCat.Open）。\n 主站总线必须已停止。 整块板卡需由本通道独占维护；其他打开通道会阻止本操作。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(BoardFileReadSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string LocalPath
    {
        get => Model.LocalPath;
        set { if (Equals(Model.LocalPath, value)) return; Model.LocalPath = value; Changed(); }
    }
    public bool IsLocalPathActive => Model.ActiveFields.Contains(nameof(BoardFileReadSetting.LocalPath));
    public Visibility LocalPathVisibility => IsLocalPathActive ? Visibility.Visible : Visibility.Collapsed;
    public string LocalPathLabel => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "本地文件路径",
        _ => ""
    };
    public string LocalPathInfo => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "本地保存目标文件路径表达式；将本次从设备读取的内容保存到此路径，不是设备端文件路径。",
        _ => ""
    };

    public string RemotePath
    {
        get => Model.RemotePath;
        set { if (Equals(Model.RemotePath, value)) return; Model.RemotePath = value; Changed(); }
    }
    public bool IsRemotePathActive => Model.ActiveFields.Contains(nameof(BoardFileReadSetting.RemotePath));
    public Visibility RemotePathVisibility => IsRemotePathActive ? Visibility.Visible : Visibility.Collapsed;
    public string RemotePathLabel => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "设备文件路径",
        _ => ""
    };
    public string RemotePathInfo => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "设备侧文件名/路径表达式，格式与权限由厂商协议/固件定义；不是本地文件路径",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(BoardFileReadSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(BoardFileReadSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        BoardFileReadOperation.BoardFileRead => "可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
