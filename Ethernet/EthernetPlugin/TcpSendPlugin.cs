using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class TcpSendPlugin : StepPluginBase<TcpSendSetting>
{
    public override string StepTypeId  => "Ethernet.TcpSend";
    public override string DisplayName => "Ethernet_TcpSend";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        通过已建立的 TCP 连接发送数据。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | 表达式(string) | 是 | "TCP1" | 已打开的连接标识名 |
        | Data | 表达式(string) | 是 | "01 02 03" | 发送数据 |
        | Encoding | 枚举 | 否 | Hex | 数据编码格式，可选值：Hex, Utf8, Ascii |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 连接不存在或发送失败时步骤报错

        ## 相关插件

        - `Ethernet_TcpOpen`：建立 TCP 连接
        - `Ethernet_TcpReceive`：接收数据
        """;

    public override IStepExecutor CreateExecutor() => new TcpSendExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"TCP Send: {s.ConnectionName} [{s.Encoding}] {s.Data}";
    }
}
