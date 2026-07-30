using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using UdpCommunicationStepPlugin.Executor;
using UdpCommunicationStepPlugin.Setting;

namespace UdpCommunicationStepPlugin;

public sealed class UdpCommunicationPlugin : StepPluginBase<UdpCommunicationSetting>
{
    public override string StepTypeId => "Example.Network.UdpCommunication";
    public override string DisplayName => "UDP 通信";
    public override string Category => "示例/网络";
    public override string IconPath => string.Empty;
    public override string Description => "发送 UTF-8 或十六进制 UDP 数据报；可等待一个响应并按任意、完全或包含方式校验。";

    public override IStepExecutor CreateExecutor() => new UdpCommunicationExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var value = DeserializeSetting(setting);
        return $"{value.OperationMode}: {value.RemoteHost}:{value.RemotePort}";
    }
}
