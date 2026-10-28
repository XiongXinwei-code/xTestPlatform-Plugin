using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class TcpClosePlugin : StepPluginBase<TcpCloseSetting>
{
    public override string StepTypeId  => "Ethernet.TcpClose";
    public override string DisplayName => "Ethernet_TCP_Close";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/tcp_close.png";

    public override string Description => """
        ## 功能

        关闭并释放指定 ConnectionName 对应的 TCP 连接。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "TCP1" | 要关闭的连接标识名，求值结果为 string |
        | TimeoutMs | int | 否 | 5000 | 可配置关闭总超时（ms，必须大于0）；等待真实任务退出与释放，不固定为5秒 |

        ## 行为

        - 从本次执行的 TCP 连接池查找 `ConnectionName`，冻结该代次、取消建立任务并确认套接字释放；独立运行与其他连接名不相互关闭
        - 超时保留真实任务与旧句柄，下一次同名打开在其打开总预算内等待旧收尾；失败释放可重试，不能假报关闭成功
        - 连接名不存在时**不报错**，按 Completed 返回

        ## 相关插件

        - `Ethernet_TcpOpen`：建立 TCP 连接
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("Ethernet", () => new TcpCloseExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"TCP Close: {s.ConnectionName}（总超时={s.TimeoutMs} ms）";
    }
}
