using Modbus.Executors;
using Modbus.Models;
using Modbus.Validation;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 批量写入插件，一次执行多个地址段的写入操作
/// </summary>
public sealed class ModbusBatchWritePlugin : StepPluginBase<ModbusBatchWriteSetting>, IStepPlugin
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
		| ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 Modbus 连接名 |
		| Items | 集合 | 是 | — | 写入项列表，元素结构见示例 |
		| IntervalMs | int | 否 | 0 | 每项写入间隔毫秒数 |

		Items 元素中 RegisterType 可选值：Coil, HoldingRegister（写入只支持这两种）；DataFormat 可选值：UInt16, Int16, UInt32_AB_CD, Int32_AB_CD, Float_AB_CD, UInt32_CD_AB, Int32_CD_AB, Float_CD_AB。

		## 行为

		- 按列表顺序逐项写入，每项之间等待 IntervalMs 毫秒
		- 任意一项写入失败则步骤报错

		## 示例

		```json
		{
		  "ConnectionName": "\"Modbus1\"",
		  "Items": [
			{ "SlaveAddress": 1, "RegisterType": "HoldingRegister", "StartAddress": 100, "Values": "500,600", "DataFormat": "UInt16" }
		  ],
		  "IntervalMs": 0
		}
		```

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

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var errors = new List<StepSettingError>();

		ModbusBatchWriteSetting s;
		try
		{
			s = DeserializeSetting(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			errors.Add(StepSettingError.Error("MB_05X", $"设置无法读取：{ex.Message}"));
			return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
		}

		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_050", "连接标识名不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
			errors.Add(StepSettingError.Error("MB_050E", $"ConnectionName 表达式无效: {connErr}"));
		if (s.Items.Count == 0)
			errors.Add(StepSettingError.Warning("MB_051", "批量写入列表为空"));
		if (s.IntervalMs < 0)
			errors.Add(StepSettingError.Error("MB_053", "写入间隔时间不能为负数"));
		for (int i = 0; i < s.Items.Count; i++)
		{
			var item = s.Items[i];
			if (string.IsNullOrWhiteSpace(item.Values))
				errors.Add(StepSettingError.Error("MB_052", $"第 {i + 1} 行：写入值不能为空"));
		}

		ModbusLifecycleValidator.CheckPrecedingConnect(
			context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}