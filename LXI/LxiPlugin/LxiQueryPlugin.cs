using LXI.Executors;
using LXI.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LXI;

public sealed class LxiQueryPlugin : StepPluginBase<LxiQuerySetting>
{
	public override string StepTypeId => "IO.LxiQuery";
	public override string DisplayName => "LXI_Query";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/LXI.StepPlugin.UI;component/Resources/Icons/lxi.png";

	public override string Description =>
		"Send SCPI query and read response (Write+Read). Setting: IpAddress(string,expression,IP), Command(string,expression,SCPI query), Terminator(string,line terminator), ReadTimeoutMs(int,read timeout ms), ResultVariable(string,target variable path).";

	public override IStepExecutor CreateExecutor() => new LxiQueryExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Query {s.IpAddress}: {s.Command} -> {s.ResultVariable}";
	}
}