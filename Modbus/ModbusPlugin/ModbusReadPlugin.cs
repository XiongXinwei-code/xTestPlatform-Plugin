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

	public override string Description => """
		## 功能

		从 Modbus 设备读取数据，支持多种寄存器类型和数据格式，读取结果存入指定变量。

		## 参数

		| 参数 | 类型 | 必填 | 默认值 | 说明 |
		|------|------|------|--------|------|
		| ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 Modbus 连接名 |
		| SlaveAddress | byte | 否 | 1 | 从站地址 |
		| RegisterType | 枚举 | 是 | HoldingRegister | 可选值：Coil, DiscreteInput, HoldingRegister, InputRegister |
		| StartAddress | 表达式(int) | 是 | — | 起始地址 |
		| Quantity | 表达式(int) | 是 | — | 读取数量 |
		| DataFormat | 枚举 | 否 | UInt16 | 可选值：UInt16, Int16, UInt32_AB_CD, Int32_AB_CD, Float_AB_CD, UInt32_CD_AB, Int32_CD_AB, Float_CD_AB |
		| ResultVariable | string(变量路径) | 是 | — | 结果存入的变量名 |

		## 行为

		- 连接不存在、从站无响应或地址非法时步骤报错
		- DataFormat 仅对寄存器类型生效，线圈/离散量返回布尔值

		## 相关插件

		- `Modbus_Connect`：建立连接
		- `Modbus_BatchRead`：批量读取多个地址段
		""";

	public override IStepExecutor CreateExecutor() => new ModbusReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Read {s.ConnectionName} Slave={s.SlaveAddress} {s.RegisterType}[{s.StartAddress}] x{s.Quantity} => {s.ResultVariable}";
	}
}
