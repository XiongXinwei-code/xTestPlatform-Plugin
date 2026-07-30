using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LXI.Models;

[MessagePackObject(true)]
public class LxiCloseSetting
{
	[ExpressionField]
	public string IpAddress { get; set; } = "\"192.168.1.100\"";
}