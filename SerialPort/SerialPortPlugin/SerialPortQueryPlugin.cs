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
		"Send data to serial port and read response (Write+Read). Setting: PortName(string,expression,port name), WriteData(string,expression,data to send), DataFormat(enum,String/Hex/Bin), ReadTimeoutMs(int,read timeout ms), ReadBytes(int,bytes to read 0=until terminator), Terminator(string), ResultVariable(string,target variable path).";

	public override IStepExecutor CreateExecutor() => new SerialPortQueryExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Query {s.PortName} ({s.DataFormat}) -> {s.ResultVariable}";
	}
}