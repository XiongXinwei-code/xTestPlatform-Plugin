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
        "Setting 字段：ConnectionName(string,表达式,已建立的OPC UA连接名), " +
        "Items(集合,节点写入列表,每个元素结构见下方JSON示例), TimeoutMs(int,超时ms,默认5000)。" +
        "Items 元素JSON示例: {\"NodeId\":\"ns=2;s=SetPoint\",\"WriteValue\":\"100.5\",\"DataType\":\"Double\"} " +
        "DataType可选值: Auto, Boolean, Int16, UInt16, Int32, UInt32, Int64, UInt64, Float, Double, String。";

    public override IStepExecutor CreateExecutor() => new OpcUaBatchWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"BatchWrite {s.Items.Count} nodes via {s.ConnectionName}";
    }
}
