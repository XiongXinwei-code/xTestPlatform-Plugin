using LXI.Executors;
using LXI.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LXI;

public sealed class LxiReadPlugin : StepPluginBase<LxiReadSetting>
{
	public override string StepTypeId => "IO.LxiRead";
	public override string DisplayName => "LXI_Read";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/LXI.StepPlugin.UI;component/Resources/Icons/lxi.png";

	public override string Description =>
		"Read response from LXI instrument and store in variable. Setting: IpAddress(string,expression,IP), ReadTimeoutMs(int,read timeout ms), Terminator(string,line terminator), ResultVariable(string,target variable path).";

	public override IStepExecutor CreateExecutor() => new LxiReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Read {s.IpAddress} -> {s.ResultVariable}";
	}
}