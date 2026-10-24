using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace SerialPort.Models;

[MessagePackObject(true)]
public class SerialPortOpenSetting
{
    [ExpressionField]
    public string PortName { get; set; } = "\"COM1\"";

    public int BaudRate { get; set; } = 9600;

    public int DataBits { get; set; } = 8;

    public int StopBits { get; set; } = 1;

    public int Parity { get; set; } = 0;

    public int ReadTimeoutMs { get; set; } = 3000;

    public int WriteTimeoutMs { get; set; } = 3000;

    /// <summary>打开总期限(ms)，包含旧资源收尾及本次操作；旧序列保留原默认行为。</summary>
    public int TimeoutMs { get; set; } = 5000;
}
