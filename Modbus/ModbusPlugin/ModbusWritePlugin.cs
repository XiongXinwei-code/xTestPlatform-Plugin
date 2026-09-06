using Modbus.Executors;
using Modbus.Models;
using Modbus.Validation;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 写入插件，支持写入线圈和保持寄存器
/// </summary>
public sealed class ModbusWritePlugin : StepPluginBase<ModbusWriteSetting>, IStepPlugin
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
		| ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 Modbus 连接名 |
		| SlaveAddress | byte | 否 | 1 | 从站地址 |
		| RegisterType | 枚举 | 是 | HoldingRegister | 可选值：Coil, HoldingRegister（写入只支持这两种） |
		| StartAddress | 表达式(int) | 是 | — | 起始地址 |
		| Values | string([ExpressionField]) | 是 | — | 要写入的值，逗号分隔，如 "100,200" |
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

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var errors = new List<StepSettingError>();

		ModbusWriteSetting s;
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
			errors.Add(StepSettingError.Error("MB_03X", $"设置无法读取：{ex.Message}"));
			return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
		}

		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_030", "连接标识名不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
			errors.Add(StepSettingError.Error("MB_030E", $"ConnectionName 表达式无效: {connErr}"));
		if (string.IsNullOrWhiteSpace(s.StartAddress))
			errors.Add(StepSettingError.Error("MB_032", "起始地址不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.StartAddress, context.ExecutionContext, out var addrErr))
			errors.Add(StepSettingError.Error("MB_032E", $"StartAddress 表达式无效: {addrErr}"));
		if (string.IsNullOrWhiteSpace(s.Values))
			errors.Add(StepSettingError.Error("MB_031", "写入值不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.Values, context.ExecutionContext, out var valErr))
			errors.Add(StepSettingError.Error("MB_031E", $"Values 表达式无效: {valErr}"));

		ModbusLifecycleValidator.CheckPrecedingConnect(
			context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}