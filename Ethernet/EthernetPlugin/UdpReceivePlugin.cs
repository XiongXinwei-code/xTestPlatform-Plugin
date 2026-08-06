using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class UdpReceivePlugin : StepPluginBase<UdpReceiveSetting>
{
    public override string StepTypeId  => "Ethernet.UdpReceive";
    public override string DisplayName => "Ethernet_UdpReceive";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description =>
        "绑定本机 UDP 端口并等待接收数据，结果可存入变量。" +
        "Setting 字段：LocalPort(int,绑定本机端口,默认30490), " +
        "BindMode(枚举,绑定模式:LocalPort/AnyInterface,默认AnyInterface), " +
        "ExpectedLength(int,期望字节数,0表示接收任意长度,默认0), " +
        "TimeoutMs(int,接收超时毫秒,默认3000), " +
        "Encoding(枚举,结果编码格式:Hex/Utf8/Ascii,默认Hex), " +
        "ResultVariable(string,结果存储变量路径,可选), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new UdpReceiveExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"UDP Receive: 端口 {s.LocalPort} [{s.BindMode}] 超时 {s.TimeoutMs}ms";
    }
}
