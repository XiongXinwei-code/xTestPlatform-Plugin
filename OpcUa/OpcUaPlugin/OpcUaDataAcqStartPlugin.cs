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

    public override string Description => """
        ## 功能

        启动 OPC UA 后台数据采集任务，按指定采样间隔定时读取多个节点并缓存数据，直到执行 DataAcq_Stop 停止并导出。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | 表达式(string) | 是 | — | 采集任务标识名，序列内唯一 |
        | ConnectionName | 表达式(string) | 是 | — | 已建立的 OPC UA 连接名 |
        | Items | 集合 | 是 | — | 采集节点列表，元素结构见示例 |
        | SamplingIntervalMs | int | 否 | 100 | 采样间隔毫秒数 |
        | MaxDurationMs | int | 否 | 0 | 最大采集时长，0 表示无限 |

        ## 行为

        - 步骤启动采集后立即返回，采集在后台持续进行
        - 同名 TaskName 已在采集中时步骤报错

        ## 示例

        ```json
        {
          "TaskName": "\"acq1\"",
          "ConnectionName": "\"OpcUa1\"",
          "Items": [
            { "NodeId": "ns=2;s=Temperature", "ColumnName": "Temp" }
          ],
          "SamplingIntervalMs": 100,
          "MaxDurationMs": 0
        }
        ```

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_DataAcq_Stop`：停止采集并导出数据
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaDataAcqStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DataAcq Start: {s.TaskName} ({s.Items.Count} nodes @ {s.SamplingIntervalMs}ms)";
    }
}
