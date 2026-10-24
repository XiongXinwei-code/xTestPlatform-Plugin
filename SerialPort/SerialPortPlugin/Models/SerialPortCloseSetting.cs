using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace SerialPort.Models;

[MessagePackObject(true)]
public class SerialPortCloseSetting
{
    [ExpressionField]
    public string PortName { get; set; } = "\"COM1\"";

    /// <summary>关闭总期限(ms)，包含旧资源收尾及本次操作；旧序列保留原默认行为。</summary>
    public int TimeoutMs { get; set; } = 5000;
}
