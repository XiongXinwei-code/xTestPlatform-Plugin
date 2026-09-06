using Modbus.Executors;
using Modbus.Models;
using Modbus.Validation;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 读取插件，支持读取线圈、离散输入、保持寄存器、输入寄存器
/// </summary>
public sealed class ModbusReadPlugin : StepPluginBase<ModbusReadSetting>, IStepPlugin
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

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var errors = new List<StepSettingError>();

		ModbusReadSetting s;
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
			errors.Add(StepSettingError.Error("MB_02X", $"设置无法读取：{ex.Message}"));
			return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
		}

		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_020", "连接标识名不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
			errors.Add(StepSettingError.Error("MB_020E", $"ConnectionName 表达式无效: {connErr}"));
		if (string.IsNullOrWhiteSpace(s.StartAddress))
			errors.Add(StepSettingError.Error("MB_024", "起始地址不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.StartAddress, context.ExecutionContext, out var addrErr))
			errors.Add(StepSettingError.Error("MB_024E", $"StartAddress 表达式无效: {addrErr}"));
		if (string.IsNullOrWhiteSpace(s.Quantity))
			errors.Add(StepSettingError.Error("MB_025", "读取数量不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.Quantity, context.ExecutionContext, out var qtyErr))
			errors.Add(StepSettingError.Error("MB_025E", $"Quantity 表达式无效: {qtyErr}"));
		if (string.IsNullOrWhiteSpace(s.ResultVariable))
			errors.Add(StepSettingError.Error("MB_021", "结果变量名不能为空"));
		else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
			errors.Add(StepSettingError.Error("MB_022", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
		else
		{
			var expectedElem = (s.RegisterType is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput)
				? typeof(bool)
				: s.DataFormat switch
				{
					ModbusDataFormat.UInt16                                        => typeof(ushort),
					ModbusDataFormat.Int16                                         => typeof(short),
					ModbusDataFormat.UInt32_AB_CD or ModbusDataFormat.UInt32_CD_AB => typeof(uint),
					ModbusDataFormat.Int32_AB_CD  or ModbusDataFormat.Int32_CD_AB  => typeof(int),
					ModbusDataFormat.Float_AB_CD  or ModbusDataFormat.Float_CD_AB  => typeof(float),
					_                                                              => typeof(ushort)
				};
			var val = context.ExecutionContext.GetVariable(s.ResultVariable);
			if (val is not null)
			{
				var valType = val.GetType();
				var elemType = valType.IsArray ? valType.GetElementType()! : valType;
				if (elemType != expectedElem)
					errors.Add(StepSettingError.Error("MB_023", $"变量 {s.ResultVariable} 类型不匹配，期望 {expectedElem.Name}，实际类型 {valType.Name}"));
			}
		}

		ModbusLifecycleValidator.CheckPrecedingConnect(
			context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}
