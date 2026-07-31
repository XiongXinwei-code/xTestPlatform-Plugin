using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

public sealed class ModbusReadPlugin : StepPluginBase<ModbusReadSetting>
{
	public override string StepTypeId => "IO.ModbusRead";
	public override string DisplayName => "Modbus_Read";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"浠?Modbus 璁惧璇诲彇鏁版嵁锛屾敮鎸佺嚎鍦堛€佺鏁ｈ緭鍏ャ€佷繚鎸佸瘎瀛樺櫒銆佽緭鍏ュ瘎瀛樺櫒銆? +
		"Setting 瀛楁锛欳onnectionName(string,琛ㄨ揪寮?, SlaveAddress(byte), RegisterType(鏋氫妇), " +
		"StartAddress(string,琛ㄨ揪寮?, Quantity(string,琛ㄨ揪寮?, DataFormat(鏋氫妇), ResultVariable(string,琛ㄨ揪寮?銆?;

	public override IStepExecutor CreateExecutor() => new ModbusReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Read {s.ConnectionName} Slave={s.SlaveAddress} {s.RegisterType}[{s.StartAddress}] x{s.Quantity} => {s.ResultVariable}";
	}
}
