using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace VISA.Models;

/// <summary>
/// VISA 读取（仅读取响应）步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class VisaReadSetting
{
    /// <summary>使用的连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "VISA1";

    /// <summary>存储读取结果的变量名</summary>
    public string ResultVariable { get; set; } = "VisaResult";

    /// <summary>是否自动去除响应中的首尾空白和终止符</summary>
    public bool TrimResponse { get; set; } = true;
}
