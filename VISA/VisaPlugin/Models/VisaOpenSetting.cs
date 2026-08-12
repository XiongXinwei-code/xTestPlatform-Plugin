using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace VISA.Models;

/// <summary>
/// VISA 打开会话步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class VisaOpenSetting
{
    /// <summary>连接名称，用于在后续步骤中引用此会话</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"VISA1\"";

    /// <summary>VISA 资源字符串，如 TCPIP::192.168.1.1::INSTR、GPIB0::1::INSTR</summary>
    [ExpressionField]
    public string ResourceString { get; set; } = "\"TCPIP::192.168.1.1::5025::SOCKET\"";

    /// <summary>打开超时时间（毫秒）</summary>
    public int OpenTimeoutMs { get; set; } = 5000;

    /// <summary>I/O 超时时间（毫秒），用于读写操作</summary>
    public int IoTimeoutMs { get; set; } = 10000;

    /// <summary>终止符，以转义文本存储（如 \n、\r\n），默认换行符</summary>
    public string Terminator { get; set; } = "\\n";
}
