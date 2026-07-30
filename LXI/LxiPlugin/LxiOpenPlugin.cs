using LXI.Executors;
using LXI.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LXI;

public sealed class LxiOpenPlugin : StepPluginBase<LxiOpenSetting>
{
	public override string StepTypeId => "IO.LxiOpen";
	public override string DisplayName => "LXI_Open";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/LXI.StepPlugin.UI;component/Resources/Icons/lxi.png";

	public override string Description =>
		"Connect to LXI/SCPI instrument via TCP. Setting: IpAddress(string,expression,IP), Port(int,default 5025), ConnectTimeoutMs(int,timeout ms), Terminator(string,line terminator).";

	public override IStepExecutor CreateExecutor() => new LxiOpenExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Connect {s.IpAddress}:{s.Port}";
	}
}