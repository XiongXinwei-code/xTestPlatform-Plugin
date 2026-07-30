using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace SerialPort.Models;

[MessagePackObject(true)]
public class SerialPortReadSetting
{
    [ExpressionField]
    public string PortName { get; set; } = "\"COM1\"";

    public SerialPortDataFormat DataFormat { get; set; } = SerialPortDataFormat.String;

    public int ReadTimeoutMs { get; set; } = 3000;

    public int ReadBytes { get; set; } = 0;

    public string Terminator { get; set; } = "\n";

    /// <summary>读取结果存放的目标变量路径（如 Locals.ReceivedData）</summary>
    public string ResultVariable { get; set; } = string.Empty;
}
