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

	/// <summary>终止符，以转义文本存储（如 \n、\r\n）；为空表示不按终止符结束，读到超时为止</summary>
	public string Terminator { get; set; } = "\\n";

	[VariablePathField]
	public string ResultVariable { get; set; } = string.Empty;
}
