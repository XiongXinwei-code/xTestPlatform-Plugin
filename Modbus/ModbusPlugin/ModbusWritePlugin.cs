using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 写入插件，支持写入线圈和保持寄存器
/// </summary>
public sealed class ModbusWritePlugin : StepPluginBase<ModbusWriteSetting>
{
	public override string StepTypeId => "IO.ModbusWrite";
	public override string DisplayName => "Modbus_Write";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"向 Modbus 设备写入数据，支持线圈和保持寄存器。" +
		"Setting 字段：ConnectionName(string,表达式), SlaveAddress(byte,从站地址), RegisterType(枚举,Coil/HoldingRegister), " +
		"StartAddress(string,表达式,起始地址), Values(string,表达式,逗号分隔), DataFormat(枚举,数据格式)。";

	public override IStepExecutor CreateExecutor() => new ModbusWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Write {s.ConnectionName} Slave={s.SlaveAddress} {s.RegisterType}[{s.StartAddress}] = {s.Values}";
	}
}