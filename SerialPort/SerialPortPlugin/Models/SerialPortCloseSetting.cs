using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace SerialPort.Models;

[MessagePackObject(true)]
public class SerialPortCloseSetting
{
    [ExpressionField]
    public string PortName { get; set; } = "\"COM1\"";

    /// <summary>关闭整个步骤总期限(ms)，包含求值、查找、SDK操作及最终发布；不改变已关闭事实。</summary>
    public int TimeoutMs { get; set; } = 5000;
}
