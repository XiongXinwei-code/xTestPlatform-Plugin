using UdpCommunication.Executors;
using UdpCommunication.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication;

public sealed class UdpOpenPlugin : StepPluginBase<UdpOpenSetting>
{
    /// <summary>UDP_Open 步骤的 StepTypeId 公开常量，给其他插件在编辑器侧按 StepType 过滤时使用。</summary>
    public const string StepTypeIdConst = "Communication.UdpOpen";

    public override string StepTypeId => StepTypeIdConst;
    public override string DisplayName => "UDP_Open";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";

    public override string Description =>
        "创建 UDP 连接并绑定到指定的本地 IP 地址和端口。\n" +
        "Setting 字段：LocalAddress(string,表达式,本地 IP，如 0.0.0.0 表示监听所有)，LocalPort(int,本地端口，1~65535)，\n" +
        "DefaultRemoteAddress(string,表达式,默认目标 IP)，DefaultRemotePort(int,默认目标端口)。";

    public override IStepExecutor CreateExecutor() => new UdpOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"UDP 打开 → {s.LocalAddress}:{s.LocalPort}";
    }
}
