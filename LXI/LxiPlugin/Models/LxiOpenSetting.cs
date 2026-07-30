using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LXI.Models;

[MessagePackObject(true)]
public class LxiOpenSetting
{
	[ExpressionField]
	public string IpAddress { get; set; } = "\"192.168.1.100\"";

	public int Port { get; set; } = 5025;

	public int ConnectTimeoutMs { get; set; } = 5000;

	public string Terminator { get; set; } = "\n";
}