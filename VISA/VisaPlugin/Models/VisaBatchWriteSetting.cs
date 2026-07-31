using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace VISA.Models;

/// <summary>
/// 批量写入的单条命令项
/// </summary>
[MessagePackObject(true)]
public class VisaBatchWriteItem
{
    /// <summary>要发送的 SCPI 命令（支持表达式）</summary>
    [ExpressionField]
    public string Command { get; set; } = "";

    /// <summary>发送后延时（毫秒），0 表示不延时</summary>
    public int DelayMs { get; set; } = 0;
}

/// <summary>
/// VISA 批量写入步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class VisaBatchWriteSetting
{
    /// <summary>使用的连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "VISA1";

    /// <summary>命令列表</summary>
    public List<VisaBatchWriteItem> Items { get; set; } = new();
}
