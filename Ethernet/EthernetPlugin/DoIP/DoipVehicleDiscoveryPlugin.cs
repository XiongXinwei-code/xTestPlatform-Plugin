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
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        通过 UDP 广播发送 DoIP 车辆识别请求，接收车辆公告并解析 VIN 与逻辑地址。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | BroadcastAddress | string([ExpressionField]) | 是 | "255.255.255.255" | 广播地址 |
        | Port | int | 否 | 13400 | UDP 发现端口 |
        | TimeoutMs | int | 否 | 3000 | 等待响应超时毫秒数 |
        | ResultVariable | string | 否 | 空 | 存储发现结果的变量路径 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 超时内未收到车辆公告时步骤报错

        ## 相关插件

        - `DoIP_Connect`：使用发现结果建立 DoIP 会话
        """;

    public override IStepExecutor CreateExecutor() => new DoipVehicleDiscoveryExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP VehicleDiscovery: {s.BroadcastAddress}:{s.Port}";
    }
}
