using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace SerialPort.Models;

[MessagePackObject(true)]
public class SerialPortCloseSetting
{
    [ExpressionField]
    public string PortName { get; set; } = "\"COM1\"";
}
