using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace UdpCommunication.Models;

[MessagePackObject(true)]
public class UdpOpenSetting
{
    /// <summary>本地 IP 地址</summary>
    [ExpressionField]
    public string LocalAddress { get; set; } = "\"0.0.0.0\"";

    /// <summary>本地端口（1~65535，0 表示系统自动分配——不推荐，可能导致后续步骤无法引用）</summary>
    public int LocalPort { get; set; } = 0;

    /// <summary>默认目标 IP 地址（可被后续步骤复用）</summary>
    [ExpressionField]
    public string DefaultRemoteAddress { get; set; } = "\"127.0.0.1\"";

    /// <summary>默认目标端口（可被后续步骤复用）</summary>
    public int DefaultRemotePort { get; set; } = 5000;
}
