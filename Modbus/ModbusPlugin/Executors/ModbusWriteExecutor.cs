using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

/// <summary>
/// Modbus 写入执行器，支持写入线圈和保持寄存器
/// </summary>
public sealed class ModbusWriteExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	/// <summary>执行 Modbus 写入操作</summary>

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusWritePlugin().CreateSerializer();
		var setting = (ModbusWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

		try
		{
			var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
			var key = ModbusHelper.GetConnectionKey(connName);

			if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not IModbusMaster master)
			{
				return new ExecutionResult
				{
					StepResult = new StepResult
					{
						Status = TestStatus.Error,
						Error = new ErrorInfo { Message = $"未找到 Modbus 连接: {connName}" }
					}
				};
			}

			var startAddr = ushort.Parse(await Evaluator.EvalStringAsync(setting.StartAddress, context));
			var valuesStr = await Evaluator.EvalStringAsync(setting.Values, context);

			if (setting.RegisterType == ModbusRegisterType.Coil)
			{
				var bools = valuesStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
					.Select(v => v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)).ToArray();
				if (bools.Length == 1)
					await master.WriteSingleCoilAsync(setting.SlaveAddress, startAddr, bools[0]);
				else
					await master.WriteMultipleCoilsAsync(setting.SlaveAddress, startAddr, bools);
			}
			else
			{
				var registers = ModbusDataConverter.ConvertToRegisters(valuesStr, setting.DataFormat);
				if (registers.Length == 1)
					await master.WriteSingleRegisterAsync(setting.SlaveAddress, startAddr, registers[0]);
				else
					await master.WriteMultipleRegistersAsync(setting.SlaveAddress, startAddr, registers);
			}

			context.LogAction?.Invoke($"Modbus Write: Addr={startAddr}, Values={valuesStr}");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已写入: Addr={startAddr}" }
			};
		}
		catch (OperationCanceledException)
		{
			return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
		}
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = $"Modbus 写入失败: {ex.Message}" }
				}
			};
		}
	}
}