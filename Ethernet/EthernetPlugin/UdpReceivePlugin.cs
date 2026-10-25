using Ethernet.Executors;
using Ethernet.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet;

public sealed class UdpReceivePlugin : StepPluginBase<UdpReceiveSetting>
{
    public override string StepTypeId  => "Ethernet.UdpReceive";
    public override string DisplayName => "Ethernet_UDP_Receive";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/udp_receive.png";

    public override string Description => """
        ## 功能

        绑定本机 UDP 端口并等待接收数据，结果可存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | LocalPort | int | 是 | 30490 | 绑定本机端口，0=系统分配临时端口；适用于对端能够获知该端口的场景 |
        | BindMode | 枚举 | 否 | AnyInterface | 绑定模式，可选值：LocalPort, AnyInterface |
        | ExpectedLength | int | 否 | 0 | 期望单个数据报字节数，范围0~65507；0表示任意长度，大于0时必须精确匹配 |
        | TimeoutMs | int | 否 | 3000 | 接收超时毫秒数 |
        | Encoding | 枚举 | 否 | Hex | 结果编码格式，可选值：Hex, Utf8, Ascii |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 结果变量名，写入类型为 string；如 Locals.result |

        ## 行为

        - ResultVariable 为空时跳过写入；非空须配置已声明的完整作用域 string 变量路径，当前值允许 null；建立/发送/消费响应前预检，不自动创建变量

        - 超时未收到数据时步骤报错
        - 临时端口登记为本次执行资源；中断或退出调试时取消接收，后台确认原始 I/O 与取消回调退出后才释放端口；首轮重跑在 TimeoutMs 内等待旧端口收尾，绑定后接收窗口仍单独使用 TimeoutMs
        - 收尾未退出时保留旧资源并返回 Error，不强行复用；旧执行收到迟到数据不能发布结果
        - ExpectedLength大于0且数据报长度不匹配时返回Error，不发布结果，不拼接多个数据报或截断单个数据报
        - LocalPort绑定模式沿用仅回环127.0.0.1的旧行为；AnyInterface绑定0.0.0.0以接收其他网卡的报文

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
