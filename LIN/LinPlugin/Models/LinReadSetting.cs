using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinReadSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"LIN1\"";

    /// <summary>过滤帧 ID（为空则接收任意 ID），支持表达式</summary>
    [ExpressionField]
    public string FilterFrameId { get; set; } = "\"\"";

    /// <summary>读取超时（毫秒）</summary>
    public int ReadTimeoutMs { get; set; } = 1000;

    /// <summary>已声明的 string 变量路径（完整十六进制数据），留空不写入；通信前校验</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = string.Empty;

    /// <summary>已声明的 string 变量路径（帧 ID 十进制字符串），留空不写入；通信前校验</summary>
    [VariablePathField]
    public string IdVariable { get; set; } = string.Empty;
}
