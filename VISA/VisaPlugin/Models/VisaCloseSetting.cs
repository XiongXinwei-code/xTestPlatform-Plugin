using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace VISA.Models;

/// <summary>
/// VISA 关闭会话步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class VisaCloseSetting
{
    /// <summary>要关闭的连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"VISA1\"";

    /// <summary>关闭总期限(ms)，包含旧资源收尾及本次操作；旧序列保留原默认行为。</summary>
    public int TimeoutMs { get; set; } = 5000;
}
