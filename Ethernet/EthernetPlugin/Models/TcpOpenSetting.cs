using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.Models;

/// <summary>Ethernet_TcpOpen 步骤设置</summary>
[MessagePackObject(true)]
public class TcpOpenSetting
{
    /// <summary>连接标识名（后续步骤通过此名引用）</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"TCP1\"";

    /// <summary>远端 IP 地址（支持表达式）</summary>
    [ExpressionField]
    public string RemoteHost { get; set; } = "\"192.168.1.1\"";

    /// <summary>远端端口号（支持表达式）</summary>
    [ExpressionField]
    public string RemotePort { get; set; } = "\"13400\"";

    /// <summary>连接超时（毫秒）</summary>
    public int ConnectTimeoutMs { get; set; } = 3000;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
