using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 断开连接插件</summary>
public sealed class OpcUaDisconnectPlugin : StepPluginBase<OpcUaDisconnectSetting>
{
    public override string StepTypeId => "OpcUa.Disconnect";
    public override string DisplayName => "OpcUa_Disconnect";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description => """
        ## 功能

        断开指定的 OPC UA 连接并释放资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 要断开的 OPC UA 连接标识名 |

        ## 行为

        - 从运行期资源表中查找 `ConnectionName` 对应的会话，关闭会话并从表中移除
        - 连接名不存在或已断开时**不报错**，仅记录日志并按 Passed 返回

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaDisconnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Disconnect {s.ConnectionName}";
    }
}
