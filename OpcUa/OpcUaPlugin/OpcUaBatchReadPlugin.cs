using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 批量读取插件</summary>
public sealed class OpcUaBatchReadPlugin : StepPluginBase<OpcUaBatchReadSetting>
{
    public override string StepTypeId => "OpcUa.BatchRead";
    public override string DisplayName => "OpcUa_BatchRead";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description =>
        "批量读取 OPC UA 服务器中多个节点的值，每个节点的结果分别存入对应变量。" +
        "Setting 字段：ConnectionName(string,表达式,已建立的OPC UA连接名), " +
        "Items(集合,节点读取列表,每个元素结构见下方JSON示例), TimeoutMs(int,超时ms,默认5000)。" +
        "Items 元素JSON示例: {\"NodeId\":\"ns=2;s=Temperature\",\"ResultVariable\":\"temp_value\"} " +
        "NodeId(string,节点标识,如ns=2;s=Temperature), ResultVariable(string,结果存入的变量名)。";

    public override IStepExecutor CreateExecutor() => new OpcUaBatchReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"BatchRead {s.Items.Count} nodes via {s.ConnectionName}";
    }
}
