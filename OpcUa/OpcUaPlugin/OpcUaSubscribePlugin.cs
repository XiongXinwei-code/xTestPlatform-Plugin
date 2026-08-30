using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 订阅插件，等待节点值满足指定条件</summary>
public sealed class OpcUaSubscribePlugin : StepPluginBase<OpcUaSubscribeSetting>
{
    public override string StepTypeId => "OpcUa.Subscribe";
    public override string DisplayName => "OpcUa_Subscribe";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description => """
        ## 功能

        订阅 OPC UA 节点并等待其值满足指定条件，用于等待 PLC/设备状态变化的场景。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 OPC UA 连接名 |
        | NodeId | string | 是 | — | 节点标识，如 ns=2;s=Status |
        | ExpectedValue | string([ExpressionField]) | 是 | — | 期望值 |
        | CompareMode | 枚举 | 否 | Equal | 可选值：Equal, NotEqual, GreaterThan, LessThan, Contains |
        | ResultVariable | string(变量路径) | 否 | 空 | 节点当前值存入的变量名 |
        | TimeoutMs | int | 否 | 10000 | 等待超时毫秒数 |
        | SamplingIntervalMs | int | 否 | 100 | 采样间隔毫秒数 |

        ## 行为

        - 节点值满足比较条件时步骤通过，超时未满足则步骤失败
        - 满足条件时的节点当前值写入 ResultVariable

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_Read`：一次性读取节点值
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaSubscribeExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Subscribe {s.NodeId} until {s.CompareMode} {s.ExpectedValue}";
    }
}
