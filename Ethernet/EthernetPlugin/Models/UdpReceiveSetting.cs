using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.Models;

/// <summary>Ethernet_UdpReceive 步骤设置</summary>
[MessagePackObject(true)]
public class UdpReceiveSetting
{
    /// <summary>绑定本机端口</summary>
    public int LocalPort { get; set; } = 30490;

    /// <summary>绑定模式</summary>
    public UdpBindMode BindMode { get; set; } = UdpBindMode.AnyInterface;

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
