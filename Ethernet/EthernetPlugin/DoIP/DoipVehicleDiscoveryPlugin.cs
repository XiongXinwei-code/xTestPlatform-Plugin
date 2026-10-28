using Ethernet.DoIP.Executors;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP;

public sealed class DoipVehicleDiscoveryPlugin : StepPluginBase<DoipVehicleDiscoverySetting>
{
    public override string StepTypeId  => "DoIP.VehicleDiscovery";
    public override string DisplayName => "DoIP_VehicleDiscovery";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/doip_vehicle_discovery.png";

    public override string Description => """
        ## 功能

        通过 UDP 广播发送 DoIP 车辆识别请求，接收车辆公告并解析 VIN 与逻辑地址。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | BroadcastAddress | string([ExpressionField]) | 是 | "255.255.255.255" | 广播地址，求值结果为 string |
        | Port | int | 否 | 13400 | UDP 发现端口 |
        | TimeoutMs | int | 否 | 3000 | 等待响应超时毫秒数 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 存储发现结果的变量路径；如 Locals.result |

        ## 行为

        - ResultVariable 为空时跳过写入；非空须配置已声明的完整作用域 string 变量路径，当前值允许 null；建立/发送/消费响应前预检，不自动创建变量

        - 超时内未收到车辆公告时步骤报错
        - 临时发现 Socket 登记为执行资源；中断或退出调试时取消通信，后台确认原始 I/O 与取消回调退出后才释放；绑定等待使用 TimeoutMs，广播后的响应窗口仍单独使用 TimeoutMs，已发送广播不表示可以撤回

        ## 相关插件

        - `DoIP_Connect`：使用发现结果建立 DoIP 会话
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("Ethernet", () => new DoipVehicleDiscoveryExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP VehicleDiscovery: {s.BroadcastAddress}:{s.Port}";
    }
}
