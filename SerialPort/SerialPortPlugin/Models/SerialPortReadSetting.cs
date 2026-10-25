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

    /// <summary>终止符，以转义文本存储（如 \n、\r\n）；为空表示不按终止符结束，读到超时为止</summary>
    public string Terminator { get; set; } = "\\n";

    /// <summary>已声明的 string 目标完整路径（如 Locals.ReceivedData）；留空不写入，通信前预检。</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = string.Empty;
}
