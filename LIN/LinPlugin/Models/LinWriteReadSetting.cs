using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinWriteReadSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"LIN1\"";

    /// <summary>发送帧 ID（0-63，支持表达式）</summary>
    [ExpressionField]
    public string FrameId { get; set; } = "0";

    /// <summary>发送数据（十六进制字符串，如 "01 02 03 04"）</summary>
    [ExpressionField]
    public string Data { get; set; } = "\"\"";

    /// <summary>校验类型</summary>
    public LinChecksumType ChecksumType { get; set; } = LinChecksumType.Enhanced;

    /// <summary>等待从机响应的超时时间（毫秒）</summary>
    public int ResponseTimeoutMs { get; set; } = 500;

    /// <summary>结果存储变量路径（存储从机响应数据，十六进制字符串）</summary>
    public string ResultVariable { get; set; } = string.Empty;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
