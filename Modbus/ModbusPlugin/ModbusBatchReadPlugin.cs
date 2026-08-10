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
		| ConnectionName | 表达式(string) | 是 | — | 已建立的 Modbus 连接名 |
		| Items | 集合 | 是 | — | 读取项列表，元素结构见示例 |
		| IntervalMs | int | 否 | 0 | 每项读取间隔毫秒数 |

		Items 元素中 RegisterType 可选值：Coil, DiscreteInput, HoldingRegister, InputRegister；DataFormat 可选值：UInt16, Int16, UInt32_AB_CD, Int32_AB_CD, Float_AB_CD, UInt32_CD_AB, Int32_CD_AB, Float_CD_AB。

		## 行为

		- 按列表顺序逐项读取，每项之间等待 IntervalMs 毫秒
		- 任意一项读取失败则步骤报错

		## 示例

		```json
		{
		  "ConnectionName": "\"Modbus1\"",
		  "Items": [
			{ "SlaveAddress": 1, "RegisterType": "HoldingRegister", "StartAddress": 0, "Quantity": 2, "DataFormat": "Float_AB_CD", "ResultVariable": "temperature" }
		  ],
		  "IntervalMs": 0
		}
		```

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