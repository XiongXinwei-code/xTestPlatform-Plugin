using MessagePack;

namespace CAN.XCP.Models;

/// <summary>XCP_Connect 步骤设置</summary>
[MessagePackObject(true)]
public class XcpConnectSetting : XcpCommonSetting
{
    /// <summary>连接模式</summary>
    public XcpConnectMode ConnectMode { get; set; } = XcpConnectMode.Normal;

    /// <summary>将 ECU 资源信息存储到变量路径</summary>
    public string ResourceVariable { get; set; } = string.Empty;
}
