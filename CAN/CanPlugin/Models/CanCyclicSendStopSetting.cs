using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
public class CanCyclicSendStopSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"CAN1\"";

    /// <summary>要停止的任务标识名</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"CyclicTask1\"";
}
