using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class TcpOpenPlugin : StepPluginBase<TcpOpenSetting>
{
    public override string StepTypeId  => "Ethernet.TcpOpen";
    public override string DisplayName => "Ethernet_TcpOpen";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description =>
        "建立 TCP 客户端连接并以 ConnectionName 注册，供后续 TcpSend/TcpReceive/TcpClose 步骤使用。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名,默认\"TCP1\"), " +
        "RemoteHost(string,表达式,远端IP地址,默认\"192.168.1.1\"), " +
        "RemotePort(string,表达式,远端端口号,默认\"13400\"), " +
        "ConnectTimeoutMs(int,连接超时毫秒,默认3000), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new TcpOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"TCP Open: {s.ConnectionName} -> {s.RemoteHost}:{s.RemotePort}";
    }
}
