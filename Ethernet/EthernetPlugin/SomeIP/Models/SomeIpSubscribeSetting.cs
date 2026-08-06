using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.SomeIP.Models;

/// <summary>SomeIp_Subscribe 步骤设置（在本地端口监听事件通知）</summary>
[MessagePackObject(true)]
public class SomeIpSubscribeSetting
{
    /// <summary>本地监听 UDP 端口（支持表达式）</summary>
    [ExpressionField]
    public string LocalPort { get; set; } = "\"30502\"";

    /// <summary>服务 ID（支持表达式，如 0x1234；用于过滤通知）</summary>
    [ExpressionField]
    public string ServiceId { get; set; } = "\"0x1234\"";

    /// <summary>事件 ID（支持表达式，如 0x8001；用于过滤通知）</summary>
    [ExpressionField]
    public string EventId { get; set; } = "\"0x8001\"";

    /// <summary>等待通知超时（毫秒）</summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>结果存储变量路径（存储通知负载十六进制字符串，可选）</summary>
    public string ResultVariable { get; set; } = string.Empty;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
