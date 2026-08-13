using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class TcpClosePlugin : StepPluginBase<TcpCloseSetting>
{
    public override string StepTypeId  => "Ethernet.TcpClose";
    public override string DisplayName => "Ethernet_TcpClose";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        关闭并释放指定 ConnectionName 对应的 TCP 连接。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "TCP1" | 要关闭的连接标识名 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 关闭后该连接名不可再被其他 TCP 步骤使用

        ## 相关插件

        - `Ethernet_TcpOpen`：建立 TCP 连接
        """;

    public override IStepExecutor CreateExecutor() => new TcpCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"TCP Close: {s.ConnectionName}";
    }
}
