using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace SerialPort.Models;

[MessagePackObject(true)]
public class SerialPortQuerySetting
{
	[ExpressionField]
	public string PortName { get; set; } = "\"COM1\"";

	[ExpressionField]
	public string WriteData { get; set; } = string.Empty;

	public SerialPortDataFormat DataFormat { get; set; } = SerialPortDataFormat.String;

	public int ReadTimeoutMs { get; set; } = 3000;

	public int ReadBytes { get; set; } = 0;

	public string Terminator { get; set; } = "\n";

	public string ResultVariable { get; set; } = string.Empty;
}