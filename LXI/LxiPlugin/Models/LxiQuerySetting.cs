using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LXI.Models;

[MessagePackObject(true)]
public class LxiQuerySetting
{
	[ExpressionField]
	public string IpAddress { get; set; } = "\"192.168.1.100\"";

	[ExpressionField]
	public string Command { get; set; } = string.Empty;

	public string Terminator { get; set; } = "\n";

	public int ReadTimeoutMs { get; set; } = 5000;

	public string ResultVariable { get; set; } = string.Empty;
}