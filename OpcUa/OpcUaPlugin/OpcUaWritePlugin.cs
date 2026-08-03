using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 写入节点插件</summary>
public sealed class OpcUaWritePlugin : StepPluginBase<OpcUaWriteSetting>
{
    public override string StepTypeId => "OpcUa.Write";
    public override string DisplayName => "OpcUa_Write";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description =>
        "向 OPC UA 服务器中单个节点写入指定值。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名), NodeId(string,表达式,节点标识), " +
        "WriteValue(string,表达式,要写入的值), DataType(枚举,Auto/Boolean/Int16/UInt16/Int32/UInt32/Int64/UInt64/Float/Double/String), " +
        "TimeoutMs(int,超时)。";

    public override IStepExecutor CreateExecutor() => new OpcUaWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write {s.NodeId} = {s.WriteValue}";
    }
}
