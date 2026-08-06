using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 读取插件，支持读取线圈、离散输入、保持寄存器、输入寄存器
/// </summary>
public sealed class ModbusReadPlugin : StepPluginBase<ModbusReadSetting>
{
	public override string StepTypeId => "IO.ModbusRead";
	public override string DisplayName => "Modbus_Read";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"从 Modbus 设备读取数据，支持多种寄存器类型和数据格式。读取结果存入 ResultVariable 指定的变量。" +
		"Setting 字段：ConnectionName(string,表达式,已建立的Modbus连接名), SlaveAddress(byte,从站地址,默认1), " +
		"RegisterType(枚举:Coil/DiscreteInput/HoldingRegister/InputRegister), " +
		"StartAddress(string,表达式,起始地址), Quantity(string,表达式,读取数量), " +
		"DataFormat(枚举:UInt16/Int16/UInt32_AB_CD/Int32_AB_CD/Float_AB_CD/UInt32_CD_AB/Int32_CD_AB/Float_CD_AB), " +
		"ResultVariable(string,结果存入的变量名)。";

	public override IStepExecutor CreateExecutor() => new ModbusReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Read {s.ConnectionName} Slave={s.SlaveAddress} {s.RegisterType}[{s.StartAddress}] x{s.Quantity} => {s.ResultVariable}";
	}
}