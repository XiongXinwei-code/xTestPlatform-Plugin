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
        | ConnectionName | string([ExpressionField]) | 是 | "OpcUa1" | 已建立的 OPC UA 连接名，求值结果为 string |
        | Items | 集合 | 是 | — | 节点读取列表，元素字段见下方 |
        | TimeoutMs | int | 否 | 5000 | 预留参数，当前执行时未使用，读取超时由连接会话控制 |

        Items 元素字段：

        - NodeId：string，节点标识（普通文本，如 ns=2;s=Temperature）
        - ResultVariable：string(VariablePathField)，该节点值写入的变量（如 Locals.temp_value），写入节点原始值，类型与节点数据类型一致

        ## 行为

        - 一次请求批量读取所有节点，任意节点状态为 Bad 则步骤报错，且所有 ResultVariable 均不写入
        - Items 为空时步骤直接通过

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
