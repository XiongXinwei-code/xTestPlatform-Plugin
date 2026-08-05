using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqTaskStartSetting
{
    /// <summary>要启动的任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"AiTask1\"";
}
