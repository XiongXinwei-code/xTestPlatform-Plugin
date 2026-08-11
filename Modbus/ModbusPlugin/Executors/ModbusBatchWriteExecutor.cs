using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

/// <summary>
/// Modbus 批量写入执行器，逐项写入多个地址段
/// </summary>
public sealed class ModbusBatchWriteExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	/// <summary>执行 Modbus 批量写入操作</summary>

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusBatchWritePlugin().CreateSerializer();
		var setting = (ModbusBatchWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

		try
		{
			var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
			var key = ModbusHelper.GetConnectionKey(connName);

			if (!context.Resources.TryGet<IModbusMaster>(key, out var master))
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

			context.LogAction?.Invoke($"Modbus 批量写入: 已写入 {setting.Items.Count} 项");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已写入 {setting.Items.Count} 项" }
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
					Error = new ErrorInfo { Message = $"Modbus 批量写入失败: {ex.Message}" }
				}
			};
		}
	}
}