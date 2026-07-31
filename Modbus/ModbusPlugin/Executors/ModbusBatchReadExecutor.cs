using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

public sealed class ModbusBatchReadExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusBatchReadPlugin().CreateSerializer();
		var setting = (ModbusBatchReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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

			var results = new List<string>();
			foreach (var item in setting.Items)
			{
				cancellationToken.ThrowIfCancellationRequested();

				object result;
				switch (item.RegisterType)
				{
					case ModbusRegisterType.Coil:
						var coils = await master.ReadCoilsAsync(item.SlaveAddress, item.StartAddress, item.Quantity);
						result = coils.Length == 1 ? coils[0] : coils;
						break;
					case ModbusRegisterType.DiscreteInput:
						var inputs = await master.ReadInputsAsync(item.SlaveAddress, item.StartAddress, item.Quantity);
						result = inputs.Length == 1 ? inputs[0] : inputs;
						break;
					case ModbusRegisterType.HoldingRegister:
						var holdRegs = await master.ReadHoldingRegistersAsync(item.SlaveAddress, item.StartAddress, item.Quantity);
						result = ModbusDataConverter.ConvertRegisters(holdRegs, item.DataFormat);
						break;
					default:
						var inRegs = await master.ReadInputRegistersAsync(item.SlaveAddress, item.StartAddress, item.Quantity);
						result = ModbusDataConverter.ConvertRegisters(inRegs, item.DataFormat);
						break;
				}

				if (!string.IsNullOrWhiteSpace(item.ResultVariable))
					context.SetVariable(item.ResultVariable, result);

				results.Add($"{item.ResultVariable}={result}");

				if (setting.IntervalMs > 0)
					await Task.Delay(setting.IntervalMs, cancellationToken);
			}

			var summary = string.Join("; ", results);
			context.LogAction?.Invoke($"Modbus BatchRead: {summary}");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = summary }
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
					Error = new ErrorInfo { Message = $"Modbus 鎵归噺璇诲彇澶辫触: {ex.Message}" }
				}
			};
		}
	}
}
