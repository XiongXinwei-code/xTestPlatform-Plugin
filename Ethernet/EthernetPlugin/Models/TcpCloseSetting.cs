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

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
