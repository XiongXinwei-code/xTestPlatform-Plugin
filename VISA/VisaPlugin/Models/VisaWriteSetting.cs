using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace VISA.Models;

/// <summary>
/// VISA 写入（发送 SCPI 命令）步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class VisaWriteSetting
{
    /// <summary>使用的连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"VISA1\"";

    /// <summary>要发送的 SCPI 命令（支持表达式）</summary>
    [ExpressionField]
    public string Command { get; set; } = "\"*IDN?\"";
}
