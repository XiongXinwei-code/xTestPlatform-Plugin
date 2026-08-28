using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.DoIP.Models;

/// <summary>DoIP_DiagRequest 步骤设置</summary>
[MessagePackObject(true)]
public class DoipDiagRequestSetting
{
    /// <summary>会话标识名</summary>
    [ExpressionField]
    public string SessionName { get; set; } = "\"DOIP1\"";

    /// <summary>目标地址（ECU 逻辑地址，支持表达式，如 0x1000）</summary>
    [ExpressionField]
    public string TargetAddress { get; set; } = "\"0x1000\"";

    /// <summary>UDS 请求数据（十六进制，支持表达式，如 \"22 F1 90\"）</summary>
    [ExpressionField]
    public string RequestData { get; set; } = "\"22 F1 90\"";

    /// <summary>响应超时（毫秒）</summary>
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>结果存储变量路径（存储响应十六进制字符串，可选）</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = string.Empty;

    /// <summary>是否输出日志</summary>
    public bool EnableLog { get; set; } = true;
}
