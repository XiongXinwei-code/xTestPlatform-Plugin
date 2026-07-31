using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

public sealed class ModbusBatchWriteExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusBatchWritePlugin().CreateSerializer();
		var setting = (ModbusBatchWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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

			foreach (var item in setting.Items)
			{
				cancellationToken.ThrowIfCancellationRequested();

				if (item.RegisterType == ModbusRegisterType.Coil)
				{
					var bools = item.Values.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
						.Select(v => v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)).ToArray();
					if (bools.Length == 1)
						await master.WriteSingleCoilAsync(item.SlaveAddress, item.StartAddress, bools[0]);
					else
						await master.WriteMultipleCoilsAsync(item.SlaveAddress, item.StartAddress, bools);
				}
				else
				{
					var registers = ModbusDataConverter.ConvertToRegisters(item.Values, item.DataFormat);
					if (registers.Length == 1)
						await master.WriteSingleRegisterAsync(item.SlaveAddress, item.StartAddress, registers[0]);
					else
						await master.WriteMultipleRegistersAsync(item.SlaveAddress, item.StartAddress, registers);
				}

				if (setting.IntervalMs > 0)
					await Task.Delay(setting.IntervalMs, cancellationToken);
			}

			context.LogAction?.Invoke($"Modbus BatchWrite: {setting.Items.Count} items written");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = $"宸插啓鍏?{setting.Items.Count} 椤? }
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
					Error = new ErrorInfo { Message = $"Modbus 鎵归噺鍐欏叆澶辫触: {ex.Message}" }
				}
			};
		}
	}
}
