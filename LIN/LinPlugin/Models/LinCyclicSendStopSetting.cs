using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinCyclicSendStopSetting
{
    /// <summary>要停止的周期任务标识名</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"LinCyclicTask1\"";
}
