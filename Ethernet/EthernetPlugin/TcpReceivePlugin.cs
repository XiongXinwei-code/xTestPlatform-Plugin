using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class TcpReceivePlugin : StepPluginBase<TcpReceiveSetting>
{
    public override string StepTypeId  => "Ethernet.TcpReceive";
    public override string DisplayName => "Ethernet_TcpReceive";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description =>
        "从已建立的 TCP 连接接收数据，结果可存入变量。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名,默认\"TCP1\"), " +
        "ExpectedLength(int,期望字节数,0表示接收任意长度,默认0), " +
        "TimeoutMs(int,接收超时毫秒,默认3000), " +
        "Encoding(枚举,结果编码格式:Hex/Utf8/Ascii,默认Hex), " +
        "ResultVariable(string,结果存储变量路径,可选), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new TcpReceiveExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var len = s.ExpectedLength > 0 ? $"{s.ExpectedLength}字节" : "任意长度";
        return $"TCP Receive: {s.ConnectionName} 接收{len} [{s.Encoding}]";
    }
}
