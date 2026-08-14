using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinSleepSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"LIN1\"";

    /// <summary>睡眠模式（Remote：发送 Go-to-Sleep 命令；Local：仅本地接口）</summary>
    public LinSleepMode SleepMode { get; set; } = LinSleepMode.Remote;

    /// <summary>入睡后延时（毫秒），等待从节点完成入睡流程；0 表示不延时</summary>
    public int PostSleepDelayMs { get; set; } = 100;
}
