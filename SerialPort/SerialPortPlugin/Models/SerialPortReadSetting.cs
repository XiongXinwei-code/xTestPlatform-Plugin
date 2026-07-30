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
}
