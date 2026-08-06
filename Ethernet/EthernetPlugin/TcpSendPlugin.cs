using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class TcpSendPlugin : StepPluginBase<TcpSendSetting>
{
    public override string StepTypeId  => "Ethernet.TcpSend";
    public override string DisplayName => "Ethernet_TcpSend";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description =>
        "通过已建立的 TCP 连接发送数据。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名,默认\"TCP1\"), " +
        "Data(string,表达式,发送数据,默认\"01 02 03\"), " +
        "Encoding(枚举,数据编码格式:Hex/Utf8/Ascii,默认Hex), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new TcpSendExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"TCP Send: {s.ConnectionName} [{s.Encoding}] {s.Data}";
    }
}
