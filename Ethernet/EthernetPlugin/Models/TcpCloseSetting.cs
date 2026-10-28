using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.Models;

/// <summary>Ethernet_TcpClose 步骤设置</summary>
[MessagePackObject(true)]
public class TcpCloseSetting
{
    /// <summary>要关闭的连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"TCP1\"";
    /// <summary>等待关闭实际完成的总期限（毫秒），必须大于0。</summary>
    public int TimeoutMs { get; set; } = 5000;
}
