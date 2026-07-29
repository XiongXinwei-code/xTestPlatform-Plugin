using MessagePack;

namespace SerialPortPlugin.Models;

[MessagePackObject(true)]
public class SerialPortCloseSetting
{
    public string PortName { get; set; } = "COM1";
}
