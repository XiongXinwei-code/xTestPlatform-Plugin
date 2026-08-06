using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.SomeIP;

public sealed class SomeIpRequestPlugin : StepPluginBase<SomeIpRequestSetting>
{
    public override string StepTypeId  => "SomeIp.Request";
    public override string DisplayName => "SomeIp_Request";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description =>
        "发送 SOME/IP RPC 请求并等待响应（支持 UDP/TCP），ReturnCode 非 0x00 或错误响应时判定为 Failed。" +
        "Setting 字段：RemoteHost(string,表达式,服务端IP,默认\"192.168.1.20\"), " +
        "RemotePort(string,表达式,服务端端口,默认\"30501\"), " +
        "Transport(枚举,传输方式 Udp/Tcp,默认Udp), " +
        "ServiceId(string,表达式,服务ID,默认\"0x1234\"), " +
        "MethodId(string,表达式,方法ID,默认\"0x0001\"), " +
        "ClientId(string,表达式,客户端ID,默认\"0x0001\"), " +
        "InterfaceVersion(string,表达式,接口版本,默认\"0x01\"), " +
        "Payload(string,表达式,十六进制负载,可为空), " +
        "TimeoutMs(int,响应超时毫秒,默认3000), " +
        "ResultVariable(string,存储响应负载的变量路径,可选), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new SomeIpRequestExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP Request: {s.RemoteHost}:{s.RemotePort} Service={s.ServiceId} Method={s.MethodId}";
    }
}
