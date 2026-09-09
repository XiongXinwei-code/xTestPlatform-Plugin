using Ethernet.DoIP.Executors;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP;

public sealed class DoipDisconnectPlugin : StepPluginBase<DoipDisconnectSetting>
{
    public override string StepTypeId  => "DoIP.Disconnect";
    public override string DisplayName => "DoIP_Disconnect";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        关闭并释放指定 SessionName 对应的 DoIP 会话。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SessionName | string([ExpressionField]) | 是 | "DOIP1" | 要关闭的会话标识名 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 从插件内部的全局 DoIP 会话池中查找 `SessionName` 对应的会话，释放连接并从会话池移除
        - 会话名不存在时**不报错**，按 Passed 返回

        ## 相关插件

        - `DoIP_Connect`：建立 DoIP 会话
        """;

    public override IStepExecutor CreateExecutor() => new DoipDisconnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP Disconnect: {s.SessionName}";
    }
}
