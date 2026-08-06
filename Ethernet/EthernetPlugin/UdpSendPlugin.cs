using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class UdpSendPlugin : StepPluginBase<UdpSendSetting>
{
    public override string StepTypeId  => "Ethernet.UdpSend";
    public override string DisplayName => "Ethernet_UdpSend";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        通过 UDP 向目标地址发送数据（无连接，每次新建 Socket）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | RemoteHost | 表达式(string) | 是 | "192.168.1.255" | 目标 IP |
        | RemotePort | 表达式(string) | 是 | "30490" | 目标端口 |
        | LocalPort | int | 否 | 0 | 本机发送端口，0=系统自动分配 |
        | Data | 表达式(string) | 是 | "01 02 03" | 发送数据 |
        | Encoding | 枚举 | 否 | Hex | 数据编码格式，可选值：Hex, Utf8, Ascii |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 无需预先建立连接，发送后立即释放 Socket

        ## 相关插件

        - `Ethernet_UdpReceive`：接收 UDP 数据
        """;

    public override IStepExecutor CreateExecutor() => new UdpSendExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"UDP Send: {s.RemoteHost}:{s.RemotePort} [{s.Encoding}]";
    }
}
