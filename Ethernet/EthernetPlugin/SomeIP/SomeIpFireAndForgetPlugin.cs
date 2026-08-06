using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP;

public sealed class SomeIpFireAndForgetPlugin : StepPluginBase<SomeIpFireAndForgetSetting>
{
    public override string StepTypeId  => "SomeIp.FireAndForget";
    public override string DisplayName => "SomeIp_FireAndForget";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description =>
        "发送 SOME/IP 无响应方法调用（RequestNoReturn，支持 UDP/TCP），发送成功即判定为 Passed。" +
        "Setting 字段：RemoteHost(string,表达式,服务端IP,默认\"192.168.1.20\"), " +
        "RemotePort(string,表达式,服务端端口,默认\"30501\"), " +
        "Transport(枚举,传输方式 Udp/Tcp,默认Udp), " +
        "ServiceId(string,表达式,服务ID,默认\"0x1234\"), " +
        "MethodId(string,表达式,方法ID,默认\"0x0001\"), " +
        "ClientId(string,表达式,客户端ID,默认\"0x0001\"), " +
        "InterfaceVersion(string,表达式,接口版本,默认\"0x01\"), " +
        "Payload(string,表达式,十六进制负载,可为空), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new SomeIpFireAndForgetExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP FireAndForget: {s.RemoteHost}:{s.RemotePort} Service={s.ServiceId} Method={s.MethodId}";
    }
}
