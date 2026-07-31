using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

public sealed class ModbusConnectPlugin : StepPluginBase<ModbusConnectSetting>
{
	public override string StepTypeId => "IO.ModbusConnect";
	public override string DisplayName => "Modbus_Connect";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"寤虹珛 Modbus 杩炴帴锛屾敮鎸?TCP 鍜?RTU(涓插彛) 涓ょ浼犺緭鏂瑰紡銆? +
		"Setting 瀛楁锛欳onnectionName(string,琛ㄨ揪寮?杩炴帴鏍囪瘑鍚?, TransportType(鏋氫妇,TCP/RTU), " +
		"IpAddress(string,琛ㄨ揪寮?TCP鍦板潃), TcpPort(int,TCP绔彛), " +
		"PortName(string,琛ㄨ揪寮?涓插彛鍚?, BaudRate(int), DataBits(int), StopBits(int), Parity(int), TimeoutMs(int)銆?;

	public override IStepExecutor CreateExecutor() => new ModbusConnectExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return s.TransportType == ModbusTransportType.TCP
			? $"Connect {s.ConnectionName} (TCP: {s.IpAddress}:{s.TcpPort})"
			: $"Connect {s.ConnectionName} (RTU: {s.PortName} @ {s.BaudRate})";
	}
}
