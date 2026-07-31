using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
public class CyclicMessageItem
{
    /// <summary>CAN ID（支持表达式，如 0x185）</summary>
    [ExpressionField]
    public string CanId { get; set; } = "0x7FF";

    /// <summary>帧类型</summary>
    public CanFrameType FrameType { get; set; } = CanFrameType.Standard;

    /// <summary>发送数据（十六进制字符串，支持表达式）</summary>
    [ExpressionField]
    public string Data { get; set; } = "FF FF FF FF FF FF FF FF";

    /// <summary>发送周期（毫秒）</summary>
    public int CycleTimeMs { get; set; } = 100;

    /// <summary>是否启用</summary>
    public bool Enabled { get; set; } = true;
}
