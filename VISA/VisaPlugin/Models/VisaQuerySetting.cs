using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace VISA.Models;

/// <summary>
/// VISA 查询（发送命令并读取响应）步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class VisaQuerySetting
{
    /// <summary>使用的连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"VISA1\"";

    /// <summary>要发送的 SCPI 命令（支持表达式）</summary>
    [ExpressionField]
    public string Command { get; set; } = "\"*IDN?\"";

    /// <summary>存储查询结果的变量名</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = "Locals.VisaResult";

    /// <summary>是否自动去除响应中的首尾空白和终止符</summary>
    public bool TrimResponse { get; set; } = true;
}
