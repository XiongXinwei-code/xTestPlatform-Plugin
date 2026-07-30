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
		"通过 TCP 连接到 LXI/SCPI 仪器。Setting 字段：IpAddress(string,表达式,仪器IP), Port(int,端口默认5025), ConnectTimeoutMs(int,连接超时ms), Terminator(string,终止符)。";

	public override IStepExecutor CreateExecutor() => new LxiOpenExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Connect {s.IpAddress}:{s.Port}";
	}
}