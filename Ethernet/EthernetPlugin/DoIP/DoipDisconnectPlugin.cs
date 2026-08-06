using Ethernet.DoIP.Executors;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP;

public sealed class DoipDisconnectPlugin : StepPluginBase<DoipDisconnectSetting>
{
    public override string StepTypeId  => "DoIP.Disconnect";
    public override string DisplayName => "DoIP_Disconnect";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description =>
        "关闭并释放指定 SessionName 对应的 DoIP 会话。" +
        "Setting 字段：SessionName(string,表达式,会话标识名,默认\"DOIP1\"), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new DoipDisconnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP Disconnect: {s.SessionName}";
    }
}
