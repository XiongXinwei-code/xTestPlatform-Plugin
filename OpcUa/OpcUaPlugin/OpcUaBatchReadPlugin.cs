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

    public override string Description => """
        ## 功能

        批量读取 OPC UA 服务器中多个节点的值，每个节点的结果分别存入对应变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 OPC UA 连接名 |
        | Items | 集合 | 是 | — | 节点读取列表，元素结构见示例 |
        | TimeoutMs | int | 否 | 5000 | 超时毫秒数 |

        ## 行为

        - 一次请求批量读取所有节点，任意节点读取失败则步骤报错

        ## 示例

        ```json
        {
          "ConnectionName": "\"OpcUa1\"",
          "Items": [
            { "NodeId": "ns=2;s=Temperature", "ResultVariable": "Locals.temp_value" }
          ],
          "TimeoutMs": 5000
        }
        ```

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_Read`：读取单个节点
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaBatchReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"BatchRead {s.Items.Count} nodes via {s.ConnectionName}";
    }
}
