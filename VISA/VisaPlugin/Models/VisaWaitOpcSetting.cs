using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace VISA.Models;

/// <summary>
/// VISA 等待操作完成步骤的设置参数（发送 *OPC? 并等待响应）
/// </summary>
[MessagePackObject(true)]
public class VisaWaitOpcSetting
{
    /// <summary>使用的连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"VISA1\"";

    /// <summary>等待超时时间（毫秒），0 表示使用会话默认 I/O 超时</summary>
    public int TimeoutMs { get; set; } = 0;
}
