using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 读取节点插件</summary>
public sealed class OpcUaReadPlugin : StepPluginBase<OpcUaReadSetting>
{
    public override string StepTypeId => "OpcUa.Read";
    public override string DisplayName => "OpcUa_Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description => """
        ## 功能

        读取 OPC UA 服务器中单个节点的值，并将结果存入指定变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 OPC UA 连接名 |
        | NodeId | string | 是 | — | 节点标识，如 ns=2;s=Temperature |
        | ResultVariable | string(变量路径) | 是 | — | 结果存入的变量名 |
        | TimeoutMs | int | 否 | 5000 | 超时毫秒数 |

        ## 行为

        - 连接不存在、节点无效或读取超时时步骤报错

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_BatchRead`：批量读取多个节点
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Read {s.NodeId} → {s.ResultVariable}";
    }
}
