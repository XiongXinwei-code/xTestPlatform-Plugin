using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.Models;

/// <summary>Ethernet_UdpSend 步骤设置</summary>
[MessagePackObject(true)]
public class UdpSendSetting
{
    /// <summary>目标 IP 地址（支持表达式）</summary>
    [ExpressionField]
    public string RemoteHost { get; set; } = "\"192.168.1.255\"";

    /// <summary>目标端口号（支持表达式）</summary>
    [ExpressionField]
    public string RemotePort { get; set; } = "\"30490\"";

    /// <summary>本机发送端口（0 = 系统自动分配）</summary>
    public int LocalPort { get; set; } = 0;

    /// <summary>发送数据（支持表达式）</summary>
    [ExpressionField]
    public string Data { get; set; } = "\"01 02 03\"";

    /// <summary>数据编码格式</summary>
    public EthernetDataEncoding Encoding { get; set; } = EthernetDataEncoding.Hex;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
