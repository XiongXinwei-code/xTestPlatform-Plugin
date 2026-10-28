using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
public class CanCloseSetting
{
    /// <summary>要关闭的连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"CAN1\"";

    /// <summary>关闭总等待期限（毫秒）。超时不等于底层退出，旧所有者仍保留。</summary>
    public int TimeoutMs { get; set; } = 5000;
}
