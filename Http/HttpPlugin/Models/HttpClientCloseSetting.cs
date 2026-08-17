using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Http.Models;

/// <summary>
/// 关闭 HTTP 客户端步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class HttpClientCloseSetting
{
    /// <summary>要释放的客户端标识名</summary>
    [ExpressionField]
    public string ClientName { get; set; } = "\"Mes\"";

    /// <summary>客户端不存在时是否忽略而不报错</summary>
    public bool IgnoreIfNotFound { get; set; } = true;
}
