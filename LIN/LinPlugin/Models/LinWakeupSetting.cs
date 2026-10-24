using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinWakeupSetting
{
    /// <summary>单次驱动调用等待上限；超时后未退出的原生调用仍保持隔离。</summary>
    public int TimeoutMs { get; set; } = 3000;
    /// <summary>要唤醒的连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"LIN1\"";

    /// <summary>唤醒模式（Remote：总线唤醒；Local：仅本地接口）</summary>
    public LinWakeupMode WakeupMode { get; set; } = LinWakeupMode.Remote;

    /// <summary>唤醒后延时（毫秒），等待从节点就绪，LIN 2.x 规范上限为 100ms</summary>
    public int PostWakeupDelayMs { get; set; } = 100;
}
