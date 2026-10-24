using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinCloseSetting
{
    /// <summary>要关闭的连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"LIN1\"";
    /// <summary>关闭总等待期限；超时仍保留真实收尾与驱动所有者。</summary>
    public int TimeoutMs { get; set; } = 3000;
}
