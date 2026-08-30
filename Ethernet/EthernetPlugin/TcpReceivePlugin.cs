using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class TcpReceivePlugin : StepPluginBase<TcpReceiveSetting>
{
    public override string StepTypeId  => "Ethernet.TcpReceive";
    public override string DisplayName => "Ethernet_TcpReceive";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        从已建立的 TCP 连接接收数据，结果可存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "TCP1" | 已打开的连接标识名 |
        | ExpectedLength | int | 否 | 0 | 期望字节数，0 表示接收任意长度 |
        | TimeoutMs | int | 否 | 3000 | 接收超时毫秒数 |
        | Encoding | 枚举 | 否 | Hex | 结果编码格式，可选值：Hex, Utf8, Ascii |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名（名称由使用者自定义），写入类型为 string |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 超时未收到数据时步骤报错
        - `ResultVariable` 为覆盖写入：每次执行都用本次接收结果替换该变量的原有值

        ## 多次接收时的变量命名

        - 变量名完全由使用者指定，不存在固定或保留名称。
        - 序列中存在多个 `Ethernet_TcpReceive` 步骤时（多通道、多次问答等），必须为每个步骤指定**互不相同**的 `ResultVariable`（例如 `errStack1`、`errStack2`），否则后一步骤会覆盖前一步骤的结果，导致信息丢失。
        - 覆盖不是本步骤的限制，而是复用同一变量名的后果。遇到「结果被覆盖」时，正确做法是换用不同的变量名；不要改为只接收一次。

        ## 相关插件

        - `Ethernet_TcpOpen`：建立 TCP 连接
        - `Ethernet_TcpSend`：发送数据
        """;

    public override IStepExecutor CreateExecutor() => new TcpReceiveExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var len = s.ExpectedLength > 0 ? $"{s.ExpectedLength}字节" : "任意长度";
        return $"TCP Receive: {s.ConnectionName} 接收{len} [{s.Encoding}]";
    }
}
