using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class UdpClosePlugin : Ethernet.PluginInfrastructure.StrictStepPluginBase<UdpCloseSetting>
{
    public override string StepTypeId => "Ethernet.UdpClose";
    public override string DisplayName => "Ethernet_UDP_Connection_Close";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/tcp_close.png";
    public override string Description => """
        ## 功能

        关闭指定命名 UDP socket 并释放端口。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "UDP1" | UDP 连接名；与 TCP 的同名资源互不影响 |
        | TimeoutMs | int | 否 | 5000 | 操作总期限，必须大于 0 |

        ## 行为

        - 关闭只依赖 UDP 连接名，不依赖此前 Send/Receive 是否成功；不存在时正常 Completed。
        - 冻结旧代次、取消等待并释放 socket；TCP 同名连接和其他 UDP 连接不受影响。
        - 超时或真实释放失败报 Error，不能把等待结束等同于资源已释放；未退出收尾由下一次 Open 的总预算等待。

        - 原调用方取消返回 Aborted；内部超时/失败返回 Error，正常完成返回 Completed。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("Ethernet", () => new UdpCloseExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"UDP Close: {s.ConnectionName} ({s.TimeoutMs} ms)";
    }
}
