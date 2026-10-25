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

    /// <summary>本次事务排队、发送、ACK 和完整响应的总超时（毫秒）</summary>
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>可选结果变量完整路径；非空时须已声明为 string（当前值允许 null），通信前预检。</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = string.Empty;
}
