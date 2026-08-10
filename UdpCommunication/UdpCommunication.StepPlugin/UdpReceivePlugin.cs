using UdpCommunication.Executors;
using UdpCommunication.Models;
using UdpCommunication.Protocol;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication;

public sealed class UdpReceivePlugin : StepPluginBase<UdpReceiveSetting>
{
    public override string StepTypeId => "Communication.UdpReceive";
    public override string DisplayName => "UDP_Receive";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";

    public override string Description =>
        "通过已打开的 UDP 连接接收数据（需要先执行 UDP_Open）。\n" +
        "Setting 字段：OpenStepAddress(string,引用的 UDP_Open 步骤地址)，\n" +
        "ReceiveTimeoutMs(int,接收超时ms)，ReplyFormat(枚举,回复格式:Utf8Text/Hexadecimal)，\n" +
        "ExpectedReply(string,表达式,期望回复)，MatchMode(枚举,匹配模式:Exact/Contains)，\n" +
        "ResponseVariable(string,回复变量路径，如 Step.UdpReply)。";

    public override IStepExecutor CreateExecutor() => new UdpReceiveExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var desc = $"UDP 接收 ← {s.OpenStepAddress}";
        if (!string.IsNullOrEmpty(s.ExpectedReply))
            desc += $"，期望 {s.MatchMode}: {UdpExecutionLog.Preview(s.ExpectedReply)}";
        return desc;
    }
}
