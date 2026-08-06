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
		"Setting 字段：ConnectionName(string,表达式,已建立的Modbus连接名), SlaveAddress(byte,从站地址,默认1), " +
		"RegisterType(枚举:Coil/HoldingRegister,写入只支持这两种), " +
		"StartAddress(string,表达式,起始地址), Values(string,表达式,要写入的值逗号分隔如'100,200'), " +
		"DataFormat(枚举:UInt16/Int16/UInt32_AB_CD/Int32_AB_CD/Float_AB_CD/UInt32_CD_AB/Int32_CD_AB/Float_CD_AB)。";

	public override IStepExecutor CreateExecutor() => new ModbusWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Write {s.ConnectionName} Slave={s.SlaveAddress} {s.RegisterType}[{s.StartAddress}] = {s.Values}";
	}
}