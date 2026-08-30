using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.UDS.Models;

[MessagePackObject(true)]
public class UdsRawRequestSetting : UdsCommonSetting
{
    /// <summary>原始请求数据（十六进制字符串，如 "10 03"）</summary>
    [ExpressionField]
    public string RequestData { get; set; } = "\"\"";

    /// <summary>是否等待响应</summary>
    public bool WaitResponse { get; set; } = true;

    /// <summary>结果变量（存储完整响应十六进制数据）</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = "";
}
