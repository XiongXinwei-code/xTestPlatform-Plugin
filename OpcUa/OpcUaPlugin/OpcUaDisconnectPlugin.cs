using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 断开连接插件</summary>
public sealed class OpcUaDisconnectPlugin : StepPluginBase<OpcUaDisconnectSetting>
{
    public override string StepTypeId => "OpcUa.Disconnect";
    public override string DisplayName => "OpcUa_Disconnect";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description =>
        "断开指定的 OPC UA 连接。" +
        "Setting 字段：ConnectionName(string,表达式,要断开的连接标识名)。";

    public override IStepExecutor CreateExecutor() => new OpcUaDisconnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Disconnect {s.ConnectionName}";
    }
}
