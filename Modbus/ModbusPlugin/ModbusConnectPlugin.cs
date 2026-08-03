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
		"建立 Modbus 连接，支持 TCP 和 RTU(串口) 两种传输方式。" +
		"Setting 字段：ConnectionName(string,表达式,连接标识名), TransportType(枚举,TCP/RTU), " +
		"IpAddress(string,表达式,TCP地址), TcpPort(int,TCP端口), " +
		"PortName(string,表达式,串口名), BaudRate(int,波特率), DataBits(int), StopBits(int), Parity(int), TimeoutMs(int,超时)。";

	public override IStepExecutor CreateExecutor() => new ModbusConnectExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return s.TransportType == ModbusTransportType.TCP
			? $"Connect {s.ConnectionName} (TCP: {s.IpAddress}:{s.TcpPort})"
			: $"Connect {s.ConnectionName} (RTU: {s.PortName} @ {s.BaudRate})";
	}
}