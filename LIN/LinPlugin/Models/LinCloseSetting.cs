using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinCloseSetting
{
    /// <summary>要关闭的连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"LIN1\"";
}
