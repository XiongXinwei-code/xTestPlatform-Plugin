using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 批量写入插件，一次执行多个地址段的写入操作
/// </summary>
public sealed class ModbusBatchWritePlugin : StepPluginBase<ModbusBatchWriteSetting>
{
	public override string StepTypeId => "IO.ModbusBatchWrite";
	public override string DisplayName => "Modbus_BatchWrite";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description => """
		## 功能

		批量写入多个 Modbus 地址段，每个项可指定不同从站、寄存器类型和数据格式。

		## 参数

		| 参数 | 类型 | 必填 | 默认值 | 说明 |
		|------|------|------|--------|------|
		| ConnectionName | string([ExpressionField]) | 是 | "Modbus1" |
		| Items | 集合 | 是 | — | 写入项列表，元素字段见下方 |
		| IntervalMs | int | 否 | 0 | 每项写入间隔毫秒数 |

		Items 元素字段：

		- SlaveAddress：byte，从站地址，默认 1
		- RegisterType：枚举，寄存器/线圈类型，写入只支持 Coil, HoldingRegister，默认 HoldingRegister
		- StartAddress：ushort，起始地址，默认 0
		- Values：string，要写入的值（普通文本，非表达式），多个值用逗号分隔，如 500,600，默认 "0"；Coil 时 1 或 true（不区分大小写）为置位，其余均为复位
		- DataFormat：枚举，数据格式，可选值：UInt16, Int16, UInt32_AB_CD, Int32_AB_CD, Float_AB_CD, UInt32_CD_AB, Int32_CD_AB, Float_CD_AB，默认 UInt16；仅对 HoldingRegister 生效

		## 行为

		- 写入前先校验所有项的 RegisterType，存在只读类型（DiscreteInput/InputRegister）时步骤直接报错，不写入任何项
		- 按列表顺序逐项写入，每项写入后等待 IntervalMs 毫秒（包括最后一项）
		- 任意一项写入失败则步骤报错，之前已写入的项不回滚
		- Items 为空时步骤直接通过

		## 相关插件

		- `Modbus_Connect`：建立连接
		- `Modbus_Write`：写入单个地址段
		""";

	public override IStepExecutor CreateExecutor() => new ModbusBatchWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"BatchWrite {s.ConnectionName} ({s.Items.Count} items)";
	}
}