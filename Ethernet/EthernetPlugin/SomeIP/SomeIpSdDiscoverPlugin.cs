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
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/someip_sd_discover.png";

    public override string Description => """
        ## 功能

        通过 UDP 组播发送 SOME/IP-SD FindService 并收集 OfferService 公告，解析服务 ID/实例 ID/版本及 IPv4 Endpoint 选项（服务实际 IP:端口/协议）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | MulticastAddress | string([ExpressionField]) | 是 | "224.244.224.245" | SD 组播地址，求值结果为 string |
        | Port | int | 否 | 30490 | SD 端口 |
        | ServiceId | string([ExpressionField]) | 否 | "0xFFFF" | 查找的服务 ID，0xFFFF 表示所有服务，求值结果为 string |
        | TimeoutMs | int | 否 | 3000 | 收集响应超时毫秒数 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 存储发现结果的变量路径；如 Locals.result |

        ## 行为

        - ResultVariable 为空时跳过写入；非空须配置已声明的完整作用域 string 变量路径，当前值允许 null；建立/发送/消费响应前预检，不自动创建变量

        - 临时发现 Socket 登记为执行资源；中断或退出调试时取消通信，后台确认原始 I/O 与取消回调退出后才释放；首轮重跑在 TimeoutMs 内等待旧绑定收尾，FindService 发送后的 Offer 收集窗口仍单独使用 TimeoutMs
        - 活动监听保留 ReuseAddress 共享语义；资源回收不等于正常收集窗口结束，旧执行不能发布发现结果

        - 超时内未发现服务时步骤返回 Error

        ## 相关插件

        - `SomeIp_Request`：向发现的服务发送 RPC 请求
        - `SomeIp_Subscribe`：监听事件通知
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("Ethernet", () => new SomeIpSdDiscoverExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP-SD Discover: {s.MulticastAddress}:{s.Port} Service={s.ServiceId}";
    }
}
