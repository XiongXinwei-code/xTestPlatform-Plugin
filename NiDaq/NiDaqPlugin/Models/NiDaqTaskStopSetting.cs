using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqTaskStopSetting
{
    /// <summary>要停止的任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"AiTask1\"";

    /// <summary>名称求值后的停止及实际收尾等待期限(ms)，不包含表达式求值。</summary>
    public int TimeoutMs { get; set; } = 5000;
}
