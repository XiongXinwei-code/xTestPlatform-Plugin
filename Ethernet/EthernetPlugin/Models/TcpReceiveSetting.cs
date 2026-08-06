using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.Models;

/// <summary>Ethernet_TcpReceive 步骤设置</summary>
[MessagePackObject(true)]
public class TcpReceiveSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"TCP1\"";

    /// <summary>期望接收字节数（0 = 接收到任意数据即返回）</summary>
    public int ExpectedLength { get; set; } = 0;

    /// <summary>接收超时（毫秒）</summary>
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>数据编码格式（结果以此格式存入变量）</summary>
    public EthernetDataEncoding Encoding { get; set; } = EthernetDataEncoding.Hex;

    /// <summary>结果存储变量路径</summary>
    public string ResultVariable { get; set; } = string.Empty;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
