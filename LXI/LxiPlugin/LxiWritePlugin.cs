using LXI.Executors;
using LXI.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LXI;

public sealed class LxiWritePlugin : StepPluginBase<LxiWriteSetting>
{
	public override string StepTypeId => "IO.LxiWrite";
	public override string DisplayName => "LXI_Write";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/LXI.StepPlugin.UI;component/Resources/Icons/lxi.png";

	public override string Description =>
		"Send SCPI command to LXI instrument (no response read). Setting: IpAddress(string,expression,IP), Command(string,expression,SCPI command), Terminator(string,line terminator).";

	public override IStepExecutor CreateExecutor() => new LxiWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Write {s.IpAddress}: {s.Command}";
	}
}