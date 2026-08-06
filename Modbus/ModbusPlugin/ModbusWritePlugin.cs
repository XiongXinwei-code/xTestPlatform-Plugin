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

	public override string Description => """
		## 功能

		向 Modbus 设备写入数据，支持线圈和保持寄存器。

		## 参数

		| 参数 | 类型 | 必填 | 默认值 | 说明 |
		|------|------|------|--------|------|
		| ConnectionName | 表达式(string) | 是 | — | 已建立的 Modbus 连接名 |
		| SlaveAddress | byte | 否 | 1 | 从站地址 |
		| RegisterType | 枚举 | 是 | HoldingRegister | 可选值：Coil, HoldingRegister（写入只支持这两种） |
		| StartAddress | 表达式(int) | 是 | — | 起始地址 |
		| Values | 表达式(string) | 是 | — | 要写入的值，逗号分隔，如 "100,200" |
		| DataFormat | 枚举 | 否 | UInt16 | 可选值：UInt16, Int16, UInt32_AB_CD, Int32_AB_CD, Float_AB_CD, UInt32_CD_AB, Int32_CD_AB, Float_CD_AB |

		## 行为

		- 连接不存在、从站无响应或写入失败时步骤报错
		- RegisterType=Coil 时 Values 按布尔解析，DataFormat 不生效

		## 相关插件

		- `Modbus_Connect`：建立连接
		- `Modbus_BatchWrite`：批量写入多个地址段
		""";

	public override IStepExecutor CreateExecutor() => new ModbusWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Write {s.ConnectionName} Slave={s.SlaveAddress} {s.RegisterType}[{s.StartAddress}] = {s.Values}";
	}
}