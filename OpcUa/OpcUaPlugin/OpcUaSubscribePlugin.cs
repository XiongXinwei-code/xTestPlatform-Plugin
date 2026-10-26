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
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua_subscribe.png";

    public override string Description => """
        ## 功能

        订阅 OPC UA 节点并等待其值满足指定条件，用于等待 PLC/设备状态变化的场景。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 OPC UA 连接名，求值结果为 string |
        | NodeId | string | 是 | — | 节点标识，如 ns=2;s=Status |
        | ExpectedValue | string([ExpressionField]) | 是 | — | 期望值，求值结果为 string |
        | CompareMode | 枚举 | 否 | Equal | 可选值：Equal, NotEqual, GreaterThan, LessThan, Contains |
        | ResultVariable | string(VariablePathField) | 否 | Locals.SubValue | 节点当前值存入的变量名；可留空不写变量 |
        | TimeoutMs | int | 否 | 10000 | 首次读取、创建、复查、等待条件与前台收尾共用总期限；停止等待不代表 SDK Create/Delete 或取消回调已退出，未完成时连接仍持有真实请求 |
        | SamplingIntervalMs | int | 否 | 500 | 采样间隔毫秒数 |

        ## 行为

        - 节点值满足比较条件时步骤通过，超时未满足则步骤失败
        - 满足条件时的节点当前值写入 ResultVariable
        - SDK 取消回调不在宿主的取消/退出线程同步运行；实际创建、删除及回调退出前，断开连接不能假报释放完成

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_Read`：一次性读取节点值
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("OpcUa", () => new OpcUaSubscribeExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Subscribe {s.NodeId} until {s.CompareMode} {s.ExpectedValue}";
    }
}
