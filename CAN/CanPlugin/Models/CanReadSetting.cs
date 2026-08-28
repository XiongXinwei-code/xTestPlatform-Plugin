using CAN.Models;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
public class CanReadSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"CAN1\"";

    /// <summary>过滤 CAN ID（为空则接收任意 ID），支持表达式</summary>
    [ExpressionField]
    public string FilterId { get; set; } = "\"\"";

    /// <summary>读取超时（毫秒）</summary>
    public int ReadTimeoutMs { get; set; } = 1000;

    /// <summary>结果存储变量路径（存储完整十六进制数据）</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = "";

    /// <summary>存储接收到的 CAN ID 的变量路径</summary>
    [VariablePathField]
    public string IdVariable { get; set; } = "";

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
