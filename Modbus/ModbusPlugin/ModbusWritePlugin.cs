using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

public sealed class ModbusWritePlugin : StepPluginBase<ModbusWriteSetting>
{
	public override string StepTypeId => "IO.ModbusWrite";
	public override string DisplayName => "Modbus_Write";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"鍚?Modbus 璁惧鍐欏叆鏁版嵁锛屾敮鎸佺嚎鍦堝拰淇濇寔瀵勫瓨鍣ㄣ€? +
		"Setting 瀛楁锛欳onnectionName(string,琛ㄨ揪寮?, SlaveAddress(byte), RegisterType(鏋氫妇,Coil/HoldingRegister), " +
		"StartAddress(string,琛ㄨ揪寮?, Values(string,琛ㄨ揪寮?閫楀彿鍒嗛殧), DataFormat(鏋氫妇)銆?;

	public override IStepExecutor CreateExecutor() => new ModbusWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Write {s.ConnectionName} Slave={s.SlaveAddress} {s.RegisterType}[{s.StartAddress}] = {s.Values}";
	}
}
