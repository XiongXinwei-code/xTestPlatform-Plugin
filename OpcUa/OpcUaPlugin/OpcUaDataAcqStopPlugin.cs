using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 数据采集停止插件</summary>
public sealed class OpcUaDataAcqStopPlugin : StepPluginBase<OpcUaDataAcqStopSetting>
{
    public override string StepTypeId => "OpcUa.DataAcqStop";
    public override string DisplayName => "OpcUa_DataAcq_Stop";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description =>
        "停止 OPC UA 后台数据采集任务，并将采集数据导出为 CSV 文件和/或统计值存入变量。" +
        "Setting 字段：TaskName(string,表达式,要停止的采集任务名), ExportFormat(枚举,Csv/Variable/Both), " +
        "CsvFilePath(string,表达式,CSV导出路径), SaveStatistics(bool,是否保存统计值), StatVariablePrefix(string,统计变量前缀)。";

    public override IStepExecutor CreateExecutor() => new OpcUaDataAcqStopExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DataAcq Stop: {s.TaskName} → {s.ExportFormat}";
    }
}
