using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 连接插件，支持 TCP 和 RTU(串口) 两种传输方式
/// </summary>
public sealed class ModbusConnectPlugin : StepPluginBase<ModbusConnectSetting>
{
	public override string StepTypeId => "IO.ModbusConnect";
	public override string DisplayName => "Modbus_Connect";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"建立 Modbus 连接，支持 TCP 和 RTU(串口) 两种传输方式。连接成功后通过 ConnectionName 标识连接，供后续 Read/Write 步骤使用。" +
		"Setting 字段：ConnectionName(string,表达式,连接标识名,默认Modbus1), TransportType(枚举:TCP/RTU), " +
		"IpAddress(string,表达式,TCP地址,默认127.0.0.1), TcpPort(int,TCP端口,默认502), " +
		"PortName(string,表达式,串口名如COM1), BaudRate(int,波特率,默认9600), DataBits(int,默认8), StopBits(int,默认1), Parity(int,默认0=None), TimeoutMs(int,超时,默认3000)。";

	public override IStepExecutor CreateExecutor() => new ModbusConnectExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return s.TransportType == ModbusTransportType.TCP
			? $"Connect {s.ConnectionName} (TCP: {s.IpAddress}:{s.TcpPort})"
			: $"Connect {s.ConnectionName} (RTU: {s.PortName} @ {s.BaudRate})";
	}
}