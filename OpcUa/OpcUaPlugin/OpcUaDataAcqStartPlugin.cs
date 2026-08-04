using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 数据采集启动插件</summary>
public sealed class OpcUaDataAcqStartPlugin : StepPluginBase<OpcUaDataAcqStartSetting>
{
    public override string StepTypeId => "OpcUa.DataAcqStart";
    public override string DisplayName => "OpcUa_DataAcq_Start";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description =>
        "启动 OPC UA 后台数据采集任务，按指定采样间隔定时读取多个节点并缓存数据，直到执行 DataAcq_Stop 停止并导出。" +
        "Setting 字段：TaskName(string,表达式,采集任务标识名), ConnectionName(string,表达式,已建立的OPC UA连接名), " +
        "Items(集合,采集节点列表,每个元素结构见下方JSON示例), SamplingIntervalMs(int,采样间隔ms,默认100), MaxDurationMs(int,最大采集时长,0=无限)。" +
        "Items 元素JSON示例: {\"NodeId\":\"ns=2;s=Temperature\",\"ColumnName\":\"Temp\"} " +
        "NodeId(string,节点标识), ColumnName(string,导出时的列名)。";

    public override IStepExecutor CreateExecutor() => new OpcUaDataAcqStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DataAcq Start: {s.TaskName} ({s.Items.Count} nodes @ {s.SamplingIntervalMs}ms)";
    }
}
