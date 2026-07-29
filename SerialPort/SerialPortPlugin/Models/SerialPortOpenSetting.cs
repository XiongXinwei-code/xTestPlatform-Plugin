using MessagePack;

namespace SerialPortPlugin.Models;

[MessagePackObject(true)]
public class SerialPortOpenSetting
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public int StopBits { get; set; } = 1;      // 0=None, 1=One, 2=Two, 3=OnePointFive
    public int Parity { get; set; } = 0;         // 0=None, 1=Odd, 2=Even, 3=Mark, 4=Space
    public int ReadTimeoutMs { get; set; } = 3000;
    public int WriteTimeoutMs { get; set; } = 3000;
}
