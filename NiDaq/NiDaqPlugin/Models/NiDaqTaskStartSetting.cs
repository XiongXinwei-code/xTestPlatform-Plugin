using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqTaskStartSetting
{
    /// <summary>要启动的任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"AiTask1\"";

    /// <summary>名称求值后的 SDK 启动及排队期限(ms)，不包含表达式求值。</summary>
    public int TimeoutMs { get; set; } = 10000;
}
