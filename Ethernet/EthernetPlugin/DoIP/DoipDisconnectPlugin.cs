using Ethernet.DoIP.Executors;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP;

public sealed class DoipDisconnectPlugin : StepPluginBase<DoipDisconnectSetting>
{
    public override string StepTypeId  => "DoIP.Disconnect";
    public override string DisplayName => "DoIP_Disconnect";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/doip_disconnect.png";

    public override string Description => """
        ## 功能

        关闭并释放指定 SessionName 对应的 DoIP 会话。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SessionName | string([ExpressionField]) | 是 | "DOIP1" | 要关闭的会话标识名，求值结果为 string |
        | TimeoutMs | int | 否 | 5000 | 可配置关闭总超时（ms，必须大于0）；等待真实任务退出与释放，不固定为5秒 |

        ## 行为

        - 从本次执行的 DoIP 会话池查找 `SessionName`，冻结该代次、取消建立任务并确认连接释放；不关闭其他会话
        - 超时/失败保留旧所有者供下一次打开有界等待或重试；不能以取消等待代表旧会话退出
        - 会话名不存在时**不报错**，按 Completed 返回

        ## 相关插件

        - `DoIP_Connect`：建立 DoIP 会话
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("Ethernet", () => new DoipDisconnectExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP Disconnect: {s.SessionName}（总超时={s.TimeoutMs} ms）";
    }
}
