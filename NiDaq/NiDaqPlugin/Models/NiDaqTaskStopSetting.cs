using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqTaskStopSetting
{
    /// <summary>要停止的任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = string.Empty;
}
