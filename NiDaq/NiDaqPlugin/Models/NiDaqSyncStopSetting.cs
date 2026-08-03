using MessagePack;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqSyncStopSetting
{
    /// <summary>要停止的采集任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = string.Empty;
}
