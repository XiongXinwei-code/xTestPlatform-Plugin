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
		"向指定串口发送数据并读取响应（Write+Read 一体操作）。" +
		"Setting 字段：PortName(string,表达式,已打开的端口名), WriteData(string,表达式,发送数据), " +
		"DataFormat(枚举:String/Hex/Bin,默认String), ReadTimeoutMs(int,读取超时ms,默认3000), " +
		"ReadBytes(int,读取字节数,0=读到终止符,默认0), Terminator(string,终止符,默认\\n), " +
		"ResultVariable(string,响应存入的变量名)。";

	public override IStepExecutor CreateExecutor() => new SerialPortQueryExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Query {s.PortName} ({s.DataFormat}) -> {s.ResultVariable}";
	}
}