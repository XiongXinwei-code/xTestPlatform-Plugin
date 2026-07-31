using UdpCommunication.StepPlugin.Executors;
using UdpCommunication.StepPlugin.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication.StepPlugin;

public sealed class UdpSendPlugin : StepPluginBase<UdpSendSetting>
{
    public override string StepTypeId => "Network.UDP_Send";
    public override string DisplayName => "UDP_Send";
    public override string Category => "Network";
    public override string IconPath => string.Empty;
    public override string Description => "向配置的 UDP 目标地址发送一帧 UTF-8 或十六进制报文。";
    public override IStepExecutor CreateExecutor() => new UdpSendExecutor(CreateSerializer());
    public override string GenerateDescription(byte[] setting) { var s = DeserializeSetting(setting); return $"UDP 发送 → {s.RemoteAddress}:{s.RemotePort}"; }
}
