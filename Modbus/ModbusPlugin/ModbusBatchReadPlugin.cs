using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 批量读取插件，一次执行多个地址段的读取操作
/// </summary>
public sealed class ModbusBatchReadPlugin : StepPluginBase<ModbusBatchReadSetting>
{
	public override string StepTypeId => "IO.ModbusBatchRead";
	public override string DisplayName => "Modbus_BatchRead";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description => """
		## 功能

		批量读取多个 Modbus 地址段，每个项可指定不同从站、寄存器类型和数据格式，每个项的读取结果分别存入对应变量。

		## 参数

		| 参数 | 类型 | 必填 | 默认值 | 说明 |
		|------|------|------|--------|------|
		| ConnectionName | string([ExpressionField]) | 是 | "Modbus1" |
		| Items | 集合 | 是 | — | 读取项列表，元素字段见下方 |
		| IntervalMs | int | 否 | 0 | 每项读取间隔毫秒数 |

		Items 元素字段：

		- SlaveAddress：byte，从站地址，默认 1
		- RegisterType：枚举，寄存器/线圈类型，可选值：Coil, DiscreteInput, HoldingRegister, InputRegister，默认 HoldingRegister
		- StartAddress：ushort，起始地址，默认 0
		- Quantity：ushort，读取数量（寄存器个数或线圈个数），默认 1；32 位格式每个值占 2 个寄存器，奇数时末尾多余的寄存器被忽略
		- DataFormat：枚举，数据解析格式，可选值：UInt16, Int16, UInt32_AB_CD, Int32_AB_CD, Float_AB_CD, UInt32_CD_AB, Int32_CD_AB, Float_CD_AB，默认 UInt16；仅对 HoldingRegister/InputRegister 生效
		- ResultVariable：string(VariablePathField)，该项读取结果写入的变量（如 Locals.temperature），为空则不写入；Coil/DiscreteInput 写入 bool（Quantity=1）或 bool[]，寄存器按 DataFormat 写入对应数值类型（单个值为标量，多个值为数组）

		## 行为

		- 按列表顺序逐项读取，每项读取后等待 IntervalMs 毫秒（包括最后一项）
		- 任意一项读取失败则步骤报错，之前已成功的项已写入变量，不回滚
		- Items 为空时步骤直接通过

		## 相关插件

		- `Modbus_Connect`：建立连接
		- `Modbus_Read`：读取单个地址段
		""";

	public override IStepExecutor CreateExecutor() => new ModbusBatchReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"BatchRead {s.ConnectionName} ({s.Items.Count} items)";
	}
}