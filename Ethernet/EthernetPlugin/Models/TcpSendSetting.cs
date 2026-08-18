using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.Models;

/// <summary>Ethernet_TcpSend 步骤设置</summary>
[MessagePackObject(true)]
public class TcpSendSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"TCP1\"";

    /// <summary>发送数据（支持表达式）</summary>
    [ExpressionField]
    public string Data { get; set; } = "\"01 02 03\"";

    /// <summary>数据编码格式</summary>
    public EthernetDataEncoding Encoding { get; set; } = EthernetDataEncoding.Hex;

    /// <summary>发送超时时间（毫秒）；0 表示使用默认值 3000ms</summary>
    public int SendTimeoutMs { get; set; } = 3000;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
