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
		"从已连接的 LXI/SCPI 仪器读取响应数据并存入变量。Setting 字段：IpAddress(string,表达式,仪器IP), ReadTimeoutMs(int,读取超时ms), Terminator(string,终止符), ResultVariable(string,结果变量路径)。";

	public override IStepExecutor CreateExecutor() => new LxiReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Read {s.IpAddress} -> {s.ResultVariable}";
	}
}