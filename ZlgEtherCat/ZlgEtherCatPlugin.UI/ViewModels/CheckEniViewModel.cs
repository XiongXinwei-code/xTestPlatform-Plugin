using System.Windows;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public sealed class CheckEniViewModel : ZlgViewModelBase<CheckEniSetting>
{
    public CheckEniOperation Operation
    {
        get => Model.Operation;
        set => SwitchOperation(value, model => model.Operation, (model, operation) => model.Operation = operation);
    }
    public IReadOnlyList<KeyValuePair<CheckEniOperation, string>> OperationChoices => [new(CheckEniOperation.CheckEni, "核对运行 ENI")];
    public string OperationTitle => Operation switch
    {
        CheckEniOperation.CheckEni => "核对运行 ENI",
        _ => "请选择有效功能"
    };
    public string OperationHelp => Operation switch
    {
        CheckEniOperation.CheckEni => "目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。\n前置步骤：加载 ENI 并启动主站（ZlgEtherCat.StartBus）。\n BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。\n返回：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。",
        _ => "当前功能未定义，不能执行。"
    };
    public string ConnectionName
    {
        get => Model.ConnectionName;
        set { if (Equals(Model.ConnectionName, value)) return; Model.ConnectionName = value; Changed(); }
    }
    public bool IsConnectionNameActive => Model.ActiveFields.Contains(nameof(CheckEniSetting.ConnectionName));
    public Visibility ConnectionNameVisibility => IsConnectionNameActive ? Visibility.Visible : Visibility.Collapsed;
    public string ConnectionNameLabel => Operation switch
    {
        CheckEniOperation.CheckEni => "连接名",
        _ => ""
    };
    public string ConnectionNameInfo => Operation switch
    {
        CheckEniOperation.CheckEni => "选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名",
        _ => ""
    };

    public string EniPath
    {
        get => Model.EniPath;
        set { if (Equals(Model.EniPath, value)) return; Model.EniPath = value; Changed(); }
    }
    public bool IsEniPathActive => Model.ActiveFields.Contains(nameof(CheckEniSetting.EniPath));
    public Visibility EniPathVisibility => IsEniPathActive ? Visibility.Visible : Visibility.Collapsed;
    public string EniPathLabel => Operation switch
    {
        CheckEniOperation.CheckEni => "ENI 文件路径",
        _ => ""
    };
    public string EniPathInfo => Operation switch
    {
        CheckEniOperation.CheckEni => "",
        _ => ""
    };

    public int TimeoutMs
    {
        get => Model.TimeoutMs;
        set { if (Equals(Model.TimeoutMs, value)) return; Model.TimeoutMs = value; NumericValueChanged(nameof(TimeoutMs)); Changed(); }
    }
    public string TimeoutMsText { get => GetNumericText(nameof(TimeoutMs), Model.TimeoutMs); set => SetIntegerText(nameof(TimeoutMs), value, parsed => TimeoutMs = parsed); }
    public bool IsTimeoutMsActive => Model.ActiveFields.Contains(nameof(CheckEniSetting.TimeoutMs));
    public Visibility TimeoutMsVisibility => IsTimeoutMsActive ? Visibility.Visible : Visibility.Collapsed;
    public string TimeoutMsLabel => Operation switch
    {
        CheckEniOperation.CheckEni => "步骤总超时（ms）",
        _ => ""
    };
    public string TimeoutMsInfo => Operation switch
    {
        CheckEniOperation.CheckEni => "包括排队、表达式和执行等待；超时不证明原生调用已退出 整数支持十进制和 0x 十六进制。 范围：1～3600000。",
        _ => ""
    };

    public string ResultVariable
    {
        get => Model.ResultVariable;
        set { if (Equals(Model.ResultVariable, value)) return; Model.ResultVariable = value; Changed(); }
    }
    public bool IsResultVariableActive => Model.ActiveFields.Contains(nameof(CheckEniSetting.ResultVariable));
    public Visibility ResultVariableVisibility => IsResultVariableActive ? Visibility.Visible : Visibility.Collapsed;
    public string ResultVariableLabel => Operation switch
    {
        CheckEniOperation.CheckEni => "结果变量",
        _ => ""
    };
    public string ResultVariableInfo => Operation switch
    {
        CheckEniOperation.CheckEni => "可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。",
        _ => ""
    };
}
