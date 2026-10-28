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
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua_batch_write.png";

    public override string Description => """
        ## 功能

        批量向 OPC UA 服务器中多个节点写入值。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "OpcUa1" | 已建立的 OPC UA 连接名，求值结果为 string |
        | Items | 集合 | 是 | — | 节点写入列表，元素字段见下方 |
        | TimeoutMs | int | 否 | 5000 | 本次批量写入的独立总期限，含 Auto 类型读取；不修改共享 Session.OperationTimeout，必须大于 0 |

        Items 元素字段：

        - NodeId：string，节点标识（普通文本，如 ns=2;s=SetPoint）
        - WriteValue：string([ExpressionField])，要写入的值，求值结果为 string；再按 DataType 转换（Auto 时先读取节点当前值并按其类型转换，当前值为空则直接写入字符串）
        - DataType：枚举，写入数据类型，可选值：Auto, Boolean, Int16, UInt16, Int32, UInt32, Int64, UInt64, Float, Double, String

        ## 行为

        - 一次请求批量写入所有节点，任意节点写入失败则步骤报错
        - Items 为空时步骤直接通过
        - Auto 与显式数值类型统一使用 InvariantCulture，小数点为 `.`；浮点不接受千位分隔、NaN 或无穷
        - 每次 Auto 读取和最终 Write 提交前检查本次总期限、调用方取消及连接关闭状态，前台已结束时不继续发后续写请求；已提交调用及取消回调仍待真实退出

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_Write`：写入单个节点
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("OpcUa", () => new OpcUaBatchWriteExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"BatchWrite {s.Items.Count} nodes via {s.ConnectionName}";
    }
}
