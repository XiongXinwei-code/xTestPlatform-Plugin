using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 批量写入插件</summary>
public sealed class OpcUaBatchWritePlugin : StepPluginBase<OpcUaBatchWriteSetting>
{
    public override string StepTypeId => "OpcUa.BatchWrite";
    public override string DisplayName => "OpcUa_BatchWrite";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description =>
        "批量向 OPC UA 服务器中多个节点写入值。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名), " +
        "Items(列表,每项含NodeId、WriteValue和DataType), TimeoutMs(int,超时)。";

    public override IStepExecutor CreateExecutor() => new OpcUaBatchWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"BatchWrite {s.Items.Count} nodes via {s.ConnectionName}";
    }
}
