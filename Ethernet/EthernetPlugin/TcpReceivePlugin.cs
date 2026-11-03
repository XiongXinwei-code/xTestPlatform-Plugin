using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class TcpReceivePlugin : Ethernet.PluginInfrastructure.StrictStepPluginBase<TcpReceiveSetting>
{
    public override string StepTypeId  => "Ethernet.TcpReceive";
    public override string DisplayName => "Ethernet_TCP_ReadWrite_Receive";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/tcp_receive.png";

    public override string Description => """
        ## 功能

        从已建立的 TCP 连接接收数据，结果可存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "TCP1" | 已打开的连接标识名，求值结果为 string |
        | UseDelimiter | bool | 否 | false | true 按分隔符接收；false 使用原 ExpectedLength 语义 |
        | DelimiterHex | string([ExpressionField]) | 条件必填 | "0D 0A" | UseDelimiter=true 时，求值为 1~64 字节十六进制串，长度不超过 MaxBytes |
        | IncludeDelimiter | bool | 否 | false | 分隔符模式是否将分隔符包含在返回值中 |
        | MaxBytes | int | 否 | 1048576 | 单条响应上限，1~16777216；分隔符计入上限，定长不得超过此值 |
        | ExpectedLength | int | 否 | 0 | 期望字节数，0 表示接收任意长度 |
        | TimeoutMs | int | 否 | 3000 | 接收超时毫秒数 |
        | Encoding | 枚举 | 否 | Hex | 结果编码格式，可选值：Hex, Utf8, Ascii |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 结果变量名（名称由使用者自定义），写入类型为 string；如 Locals.result |

        ## 行为

        - ResultVariable 为空时跳过写入；非空须配置已声明的完整作用域 string 变量路径，当前值允许 null；建立/发送/消费响应前预检，不自动创建变量

        - 同一 TCP 连接的读取串行，排队与完整响应共用 TimeoutMs；TCP 是字节流，不把单次 socket.Read 当成协议边界。
        - 分隔符跨网络片段也能识别；一次接收中超过分隔符的尾部保留给同一连接的下一次读取，支持分隔符/定长/任意块之间切换。
        - 超时或取消不发布结果，已接收但尚不完整的数据仍保留在本连接缓冲；新事务若要丢弃该响应，先关闭并重开连接。新连接不会继承旧缓冲。
        - 超过 MaxBytes 或对端关闭而未得到完整响应时报错；可关闭连接清空缓冲。
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

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("Ethernet", () => new TcpReceiveExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var len = s.UseDelimiter ? $"分隔符 {s.DelimiterHex}" : s.ExpectedLength > 0 ? $"{s.ExpectedLength}字节" : "任意长度";
        return $"TCP Receive: {s.ConnectionName} 接收{len} [{s.Encoding}]";
    }
}
