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

    public override string Description =>
        "通过 UDP 广播发送 DoIP 车辆识别请求，接收车辆公告并解析 VIN 与逻辑地址。" +
        "Setting 字段：BroadcastAddress(string,表达式,广播地址,默认\"255.255.255.255\"), " +
        "Port(int,UDP发现端口,默认13400), " +
        "TimeoutMs(int,等待响应超时毫秒,默认3000), " +
        "ResultVariable(string,存储发现结果的变量路径,可选), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new DoipVehicleDiscoveryExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP VehicleDiscovery: {s.BroadcastAddress}:{s.Port}";
    }
}
