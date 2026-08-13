using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class TcpOpenPlugin : StepPluginBase<TcpOpenSetting>
{
    public override string StepTypeId  => "Ethernet.TcpOpen";
    public override string DisplayName => "Ethernet_TcpOpen";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        建立 TCP 客户端连接并以 ConnectionName 注册，供后续 TcpSend/TcpReceive/TcpClose 步骤使用。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "TCP1" | 连接标识名 |
        | RemoteHost | string([ExpressionField]) | 是 | "192.168.1.1" | 远端 IP 地址 |
        | RemotePort | string([ExpressionField]) | 是 | "13400" | 远端端口号 |
        | ConnectTimeoutMs | int | 否 | 3000 | 连接超时毫秒数 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 连接失败或超时时步骤报错

        ## 相关插件

        - `Ethernet_TcpSend` / `Ethernet_TcpReceive`：收发数据
        - `Ethernet_TcpClose`：关闭连接
        """;

    public override IStepExecutor CreateExecutor() => new TcpOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"TCP Open: {s.ConnectionName} -> {s.RemoteHost}:{s.RemotePort}";
    }
}
