using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace VISA.Models;

/// <summary>
/// VISA 关闭会话步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class VisaCloseSetting
{
    /// <summary>要关闭的连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"VISA1\"";
}
