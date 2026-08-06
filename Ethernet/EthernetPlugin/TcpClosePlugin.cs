using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class TcpClosePlugin : StepPluginBase<TcpCloseSetting>
{
    public override string StepTypeId  => "Ethernet.TcpClose";
    public override string DisplayName => "Ethernet_TcpClose";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description =>
        "关闭并释放指定 ConnectionName 对应的 TCP 连接。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名,默认\"TCP1\"), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new TcpCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"TCP Close: {s.ConnectionName}";
    }
}
