using LXI.Executors;
using LXI.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LXI;

public sealed class LxiClosePlugin : StepPluginBase<LxiCloseSetting>
{
	public override string StepTypeId => "IO.LxiClose";
	public override string DisplayName => "LXI_Close";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/LXI.StepPlugin.UI;component/Resources/Icons/lxi.png";

	public override string Description =>
		"断开与 LXI/SCPI 仪器的 TCP 连接。Setting 字段：IpAddress(string,表达式,仪器IP)。";

	public override IStepExecutor CreateExecutor() => new LxiCloseExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Disconnect {s.IpAddress}";
	}
}