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
		"向已连接的 LXI/SCPI 仪器发送查询命令并读取响应（Write+Read）。Setting 字段：IpAddress(string,表达式,仪器IP), Command(string,表达式,SCPI查询命令), Terminator(string,终止符), ReadTimeoutMs(int,读取超时ms), ResultVariable(string,结果变量路径)。";

	public override IStepExecutor CreateExecutor() => new LxiQueryExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Query {s.IpAddress}: {s.Command} -> {s.ResultVariable}";
	}
}