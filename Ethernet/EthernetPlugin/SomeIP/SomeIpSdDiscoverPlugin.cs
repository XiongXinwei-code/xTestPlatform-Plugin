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

    public override string Description =>
        "通过 UDP 组播发送 SOME/IP-SD FindService 并收集 OfferService 公告，解析服务ID/实例ID/版本及 IPv4 Endpoint 选项（服务实际 IP:端口/协议）；超时内未发现服务判定为 Failed。" +
        "Setting 字段：MulticastAddress(string,表达式,SD组播地址,默认\"224.244.224.245\"), " +
        "Port(int,SD端口,默认30490), " +
        "ServiceId(string,表达式,查找的服务ID,0xFFFF表示所有服务,默认\"0xFFFF\"), " +
        "TimeoutMs(int,收集响应超时毫秒,默认3000), " +
        "ResultVariable(string,存储发现结果的变量路径,可选), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new SomeIpSdDiscoverExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP-SD Discover: {s.MulticastAddress}:{s.Port} Service={s.ServiceId}";
    }
}
