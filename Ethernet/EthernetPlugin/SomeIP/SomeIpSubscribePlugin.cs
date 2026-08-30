using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP;

public sealed class SomeIpSubscribePlugin : StepPluginBase<SomeIpSubscribeSetting>
{
    public override string StepTypeId  => "SomeIp.Subscribe";
    public override string DisplayName => "SomeIp_Subscribe";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        在本地 UDP 端口监听 SOME/IP 事件通知（Notification），按 ServiceId/EventId 过滤。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | LocalPort | string([ExpressionField]) | 是 | "30502" | 本地监听 UDP 端口 |
        | ServiceId | string([ExpressionField]) | 是 | "0x1234" | 服务 ID 过滤 |
        | EventId | string([ExpressionField]) | 是 | "0x8001" | 事件 ID 过滤 |
        | TimeoutMs | int | 否 | 5000 | 等待通知超时毫秒数 |
        | ResultVariable | string(变量路径) | 否 | 空 | 存储通知负载的变量路径 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 超时未收到匹配通知时步骤判定为 Error

        ## 相关插件

        - `SomeIp_SdDiscover`：发现服务及其 Endpoint
        - `SomeIp_Request`：RPC 请求
        """;

    public override IStepExecutor CreateExecutor() => new SomeIpSubscribeExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP Subscribe: 端口 {s.LocalPort} Service={s.ServiceId} Event={s.EventId}";
    }
}
