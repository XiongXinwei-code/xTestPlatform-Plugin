using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinWriteSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"LIN1\"";

    /// <summary>LIN 帧 ID（0-63，支持表达式）</summary>
    [ExpressionField]
    public string FrameId { get; set; } = "0";

    /// <summary>发送数据（十六进制字符串，如 "01 02 03 04"）</summary>
    [ExpressionField]
    public string Data { get; set; } = "\"\"";

    /// <summary>校验类型</summary>
    public LinChecksumType ChecksumType { get; set; } = LinChecksumType.Enhanced;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
