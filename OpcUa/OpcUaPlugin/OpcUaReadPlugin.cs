using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 读取节点插件</summary>
public sealed class OpcUaReadPlugin : StepPluginBase<OpcUaReadSetting>
{
    public override string StepTypeId => "OpcUa.Read";
    public override string DisplayName => "OpcUa_Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description =>
        "读取 OPC UA 服务器中单个节点的值，并将结果存入指定变量。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名), NodeId(string,表达式,节点标识如ns=2;s=Temperature), " +
        "ResultVariable(string,结果变量名), TimeoutMs(int,超时)。";

    public override IStepExecutor CreateExecutor() => new OpcUaReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Read {s.NodeId} → {s.ResultVariable}";
    }
}
