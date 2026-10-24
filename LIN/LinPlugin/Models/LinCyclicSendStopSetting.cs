using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinCyclicSendStopSetting
{
    /// <summary>登记门与周期任务退出共用总预算，默认3000ms；超时保留任务与隔离，不报告已停止</summary>
    public int TimeoutMs { get; set; } = 3000;
    /// <summary>要停止的周期任务标识名</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"LinCyclicTask1\"";
}
