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

    public override string Description => """
        ## 功能

        批量向 OPC UA 服务器中多个节点写入值。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 OPC UA 连接名 |
        | Items | 集合 | 是 | — | 节点写入列表，元素结构见示例 |
        | TimeoutMs | int | 否 | 5000 | 超时毫秒数 |

        Items 元素中 DataType 可选值：Auto, Boolean, Int16, UInt16, Int32, UInt32, Int64, UInt64, Float, Double, String。

        ## 行为

        - 一次请求批量写入所有节点，任意节点写入失败则步骤报错

        ## 示例

        ```json
        {
          "ConnectionName": "\"OpcUa1\"",
          "Items": [
            { "NodeId": "ns=2;s=SetPoint", "WriteValue": "100.5", "DataType": "Double" }
          ],
          "TimeoutMs": 5000
        }
        ```

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_Write`：写入单个节点
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaBatchWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"BatchWrite {s.Items.Count} nodes via {s.ConnectionName}";
    }
}
