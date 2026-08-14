using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinWakeupSetting
{
    /// <summary>要唤醒的连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"LIN1\"";

    /// <summary>唤醒模式（Remote：总线唤醒；Local：仅本地接口）</summary>
    public LinWakeupMode WakeupMode { get; set; } = LinWakeupMode.Remote;
}
