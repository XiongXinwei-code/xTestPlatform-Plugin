using UdpCommunication.Executors;
using UdpCommunication.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication;

public sealed class UdpClosePlugin : StepPluginBase<UdpCloseSetting>
{
    public override string StepTypeId => "Communication.UdpClose";
    public override string DisplayName => "UDP_Close";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";

    public override string Description =>
        "关闭指定的 UDP 连接。\n" +
        "Setting 字段：OpenStepAddress(string,引用的 UDP_Open 步骤地址)。";

    public override IStepExecutor CreateExecutor() => new UdpCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"UDP 关闭 → {s.OpenStepAddress}";
    }
}
