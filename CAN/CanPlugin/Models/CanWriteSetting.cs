using CAN.Models;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
public class CanWriteSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "CAN1";

    /// <summary>CAN ID（支持表达式，如 0x7DF）</summary>
    [ExpressionField]
    public string CanId { get; set; } = "0x7DF";

    /// <summary>帧类型</summary>
    public CanFrameType FrameType { get; set; } = CanFrameType.Standard;

    /// <summary>发送数据（十六进制字符串，如 "02 10 01 00 00 00 00 00"）</summary>
    [ExpressionField]
    public string Data { get; set; } = "";

    /// <summary>是否使用 CAN FD 帧格式发送</summary>
    public bool UseFdFrame { get; set; } = false;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
