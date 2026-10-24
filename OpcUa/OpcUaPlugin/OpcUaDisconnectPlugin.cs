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
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua_disconnect.png";

    public override string Description => """
        ## 功能

        断开指定的 OPC UA 连接并释放资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 要断开的 OPC UA 连接标识名，求值结果为 string |
        | TimeoutMs | int | 否 | 5000 | 名称求值后的断开阶段总期限（毫秒），包含登记排队及原 Session/后台活动真实退出；不包含表达式求值 |

        ## 行为


        - 先冻结当前代次的新操作，再等待真实任务和 SDK 释放；只在确认释放后移除自己的资源。超时/取消不代表底层已退出，未完成或关闭失败的 owner 保留诊断与重试入口；立即重跑将按打开/配置步骤期限等待旧收尾，不强制关闭其他活动连接。

        - 从运行期资源表中查找 `ConnectionName` 对应的会话，关闭会话并从表中移除
        - 连接名不存在或已断开时**不报错**，仅记录日志并按 Completed 返回

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
