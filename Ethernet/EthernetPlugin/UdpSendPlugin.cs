using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class UdpSendPlugin : StepPluginBase<UdpSendSetting>
{
    public override string StepTypeId  => "Ethernet.UdpSend";
    public override string DisplayName => "Ethernet_UDP_Send";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/udp_send.png";

    public override string Description => """
        ## 功能

        通过 UDP 向目标地址发送数据（无连接，每次新建 Socket）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | RemoteHost | string([ExpressionField]) | 是 | "192.168.1.255" | 目标 IP，求值结果为 string |
        | RemotePort | string([ExpressionField]) | 是 | "30490" | 目标端口，求值结果为 string |
        | LocalPort | int | 否 | 0 | 本机发送端口，0=系统自动分配 |
        | SendTimeoutMs | int | 否 | 3000 | 单次发送总超时（ms），必须大于 0；旧序列缺省为 3000 |
        | Data | string([ExpressionField]) | 是 | "01 02 03" | 发送数据，求值结果为 string |
        | Encoding | 枚举 | 否 | Hex | 数据编码格式，可选值：Hex, Utf8, Ascii |

        ## 行为

        - 无需预先建立连接；等待真实 Socket 发送调用退出后释放本次 Socket
        - 临时 Socket 登记为本次执行资源；中断或退出调试时请求取消，后台确认原始 I/O 与取消回调退出后才释放；同端口重跑等待旧收尾计入原 SendTimeoutMs 总预算
        - 超过 SendTimeoutMs 返回 Error；只有原调用方取消返回 Aborted。开始提交后的取消/超时不证明报文被撤回，送达状态未知，不自动重发
        - Completed 仅表示本机发送调用成功，不表示远端已接收或执行

        ## 相关插件

        - `Ethernet_UdpReceive`：接收 UDP 数据
        """;

    public override IStepExecutor CreateExecutor() => new UdpSendExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"UDP Send: {s.RemoteHost}:{s.RemotePort} [{s.Encoding}], 总超时={s.SendTimeoutMs} ms";
    }
}
