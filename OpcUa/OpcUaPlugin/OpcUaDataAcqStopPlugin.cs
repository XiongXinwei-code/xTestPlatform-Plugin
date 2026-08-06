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

    public override string Description => """
        ## 功能

        停止 OPC UA 后台数据采集任务，并将采集数据导出为 CSV 文件和/或统计值存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | 表达式(string) | 是 | — | 要停止的采集任务名 |
        | ExportFormat | 枚举 | 是 | Csv | 可选值：Csv, Variable, Both |
        | CsvFilePath | 表达式(string) | 含 Csv 时 | — | CSV 导出路径 |
        | SaveStatistics | bool | 否 | false | 是否保存统计值（最大/最小/均值等）到变量 |
        | StatVariablePrefix | string | 否 | 空 | 统计变量前缀，SaveStatistics=true 时使用 |

        ## 行为

        - 停止后台采集并按 ExportFormat 导出数据
        - 任务不存在或导出失败时步骤报错

        ## 相关插件

        - `OpcUa_DataAcq_Start`：启动采集任务
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaDataAcqStopExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DataAcq Stop: {s.TaskName} → {s.ExportFormat}";
    }
}
