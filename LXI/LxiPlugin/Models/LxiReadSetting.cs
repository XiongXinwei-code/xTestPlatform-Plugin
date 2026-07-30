using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LXI.Models;

[MessagePackObject(true)]
public class LxiReadSetting
{
	[ExpressionField]
	public string IpAddress { get; set; } = "\"192.168.1.100\"";

	public int ReadTimeoutMs { get; set; } = 5000;

	public string Terminator { get; set; } = "\n";

	public string ResultVariable { get; set; } = string.Empty;
}