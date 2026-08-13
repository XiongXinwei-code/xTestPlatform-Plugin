using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.DoIP.Models;

/// <summary>DoIP_Connect 步骤设置</summary>
[MessagePackObject(true)]
public class DoipConnectSetting
{
    /// <summary>会话标识名（后续步骤通过此名引用）</summary>
    [ExpressionField]
    public string SessionName { get; set; } = "\"DOIP1\"";

    /// <summary>DoIP 实体 IP 地址（支持表达式）</summary>
    [ExpressionField]
    public string RemoteHost { get; set; } = "\"192.168.1.10\"";

    /// <summary>DoIP TCP 端口（支持表达式，默认 13400）</summary>
    [ExpressionField]
    public string RemotePort { get; set; } = "\"13400\"";

    /// <summary>源地址（诊断仪逻辑地址，如 0x0E00）</summary>
    public string SourceAddress { get; set; } = "0x0E00";

    /// <summary>路由激活类型</summary>
    public DoipActivationType ActivationType { get; set; } = DoipActivationType.Default;

    /// <summary>连接与响应超时（毫秒）</summary>
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
