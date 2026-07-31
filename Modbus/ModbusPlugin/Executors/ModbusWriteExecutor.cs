using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

public sealed class ModbusWriteExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusWritePlugin().CreateSerializer();
		var setting = (ModbusWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

		try
		{
			var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
			var key = ModbusHelper.GetConnectionKey(connName);

			if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not IModbusMaster master)
			{
				return new ExecutionResult
				{
					StepResult = new StepResult
					{
						Status = TestStatus.Error,
						Error = new ErrorInfo { Message = $"鏈壘鍒?Modbus 杩炴帴: {connName}" }
					}
				};
			}

			var startAddr = ushort.Parse(await Evaluator.EvaluateAsync<string>(setting.StartAddress, context) ?? setting.StartAddress);
			var valuesStr = await Evaluator.EvaluateAsync<string>(setting.Values, context) ?? setting.Values;

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
				StepResult = new StepResult { Status = TestStatus.Passed, Value = $"宸插啓鍏? Addr={startAddr}" }
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
					Error = new ErrorInfo { Message = $"Modbus 鍐欏叆澶辫触: {ex.Message}" }
				}
			};
		}
	}
}
