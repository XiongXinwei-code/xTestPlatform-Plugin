using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.XCP.Models;

/// <summary>XCP 公共设置基类（CAN 传输层参数）</summary>
[MessagePackObject(true)]
public class XcpCommonSetting
{
    /// <summary>已打开的 CAN 连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"CAN1\"";

    /// <summary>XCP 请求 CAN ID（主 → 从）</summary>
    [ExpressionField]
    public string TxId { get; set; } = "\"0x7E1\"";

    /// <summary>XCP 响应 CAN ID（从 → 主）</summary>
    [ExpressionField]
    public string RxId { get; set; } = "\"0x7E9\"";

    /// <summary>响应超时（毫秒）</summary>
    public int TimeoutMs { get; set; } = 1000;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
