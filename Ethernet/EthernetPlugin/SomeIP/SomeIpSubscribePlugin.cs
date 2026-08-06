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

    public override string Description =>
        "在本地 UDP 端口监听 SOME/IP 事件通知（Notification），按 ServiceId/EventId 过滤，超时未收到则判定为 Error。" +
        "Setting 字段：LocalPort(string,表达式,本地监听UDP端口,默认\"30502\"), " +
        "ServiceId(string,表达式,服务ID过滤,默认\"0x1234\"), " +
        "EventId(string,表达式,事件ID过滤,默认\"0x8001\"), " +
        "TimeoutMs(int,等待通知超时毫秒,默认5000), " +
        "ResultVariable(string,存储通知负载的变量路径,可选), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new SomeIpSubscribeExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOME/IP Subscribe: 端口 {s.LocalPort} Service={s.ServiceId} Event={s.EventId}";
    }
}
