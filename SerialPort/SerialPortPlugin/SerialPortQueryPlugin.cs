using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPort;

public sealed class SerialPortQueryPlugin : StepPluginBase<SerialPortQuerySetting>
{
	public override string StepTypeId => "IO.SerialPortQuery";
	public override string DisplayName => "SerialPort_Query";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

	public override string Description =>
		"向指定串口发送数据并读取响应（Write+Read）。Setting 字段：PortName(string,表达式,端口名), WriteData(string,表达式,发送数据), DataFormat(enum,数据格式:String/Hex/Bin), ReadTimeoutMs(int,读取超时ms), ReadBytes(int,读取字节数0=直到终止符), Terminator(string,终止符), ResultVariable(string,结果变量路径)。";

	public override IStepExecutor CreateExecutor() => new SerialPortQueryExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Query {s.PortName} ({s.DataFormat}) -> {s.ResultVariable}";
	}
}