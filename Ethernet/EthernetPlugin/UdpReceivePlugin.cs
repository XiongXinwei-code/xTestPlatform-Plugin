using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class UdpReceivePlugin : StepPluginBase<UdpReceiveSetting>
{
    public override string StepTypeId  => "Ethernet.UdpReceive";
    public override string DisplayName => "Ethernet_UdpReceive";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        绑定本机 UDP 端口并等待接收数据，结果可存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | LocalPort | int | 是 | 30490 | 绑定本机端口 |
        | BindMode | 枚举 | 否 | AnyInterface | 绑定模式，可选值：LocalPort, AnyInterface |
        | ExpectedLength | int | 否 | 0 | 期望字节数，0 表示接收任意长度 |
        | TimeoutMs | int | 否 | 3000 | 接收超时毫秒数 |
        | Encoding | 枚举 | 否 | Hex | 结果编码格式，可选值：Hex, Utf8, Ascii |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，写入类型为 string |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 超时未收到数据时步骤报错

        ## 相关插件

        - `Ethernet_UdpSend`：发送 UDP 数据
        """;

    public override IStepExecutor CreateExecutor() => new UdpReceiveExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"UDP Receive: 端口 {s.LocalPort} [{s.BindMode}] 超时 {s.TimeoutMs}ms";
    }
}
