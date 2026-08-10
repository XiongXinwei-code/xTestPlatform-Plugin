using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP;

public sealed class SomeIpSdDiscoverPlugin : StepPluginBase<SomeIpSdDiscoverSetting>
{
    public override string StepTypeId  => "SomeIp.SdDiscover";
    public override string DisplayName => "SomeIp_SdDiscover";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        通过 UDP 组播发送 SOME/IP-SD FindService 并收集 OfferService 公告，解析服务 ID/实例 ID/版本及 IPv4 Endpoint 选项（服务实际 IP:端口/协议）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | MulticastAddress | 表达式(string) | 是 | "224.244.224.245" | SD 组播地址 |
        | Port | int | 否 | 30490 | SD 端口 |
        | ServiceId | 表达式(string) | 否 | "0xFFFF" | 查找的服务 ID，0xFFFF 表示所有服务 |
        | TimeoutMs | int | 否 | 3000 | 收集响应超时毫秒数 |
        | ResultVariable | string | 否 | 空 | 存储发现结果的变量路径 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 超时内未发现服务时步骤判定为 Failed

        ## 相关插件

        - `SomeIp_Request`：向发现的服务发送 RPC 请求
        - `SomeIp_Subscribe`：监听事件通知
        """;

    public override IStepExecutor CreateExecutor() => new SomeIpSdDiscoverExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP-SD Discover: {s.MulticastAddress}:{s.Port} Service={s.ServiceId}";
    }
}
