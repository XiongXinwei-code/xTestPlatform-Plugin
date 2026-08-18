using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

/// <summary>
/// Modbus 批量读取执行器，逐项读取多个地址段并将结果存入变量
/// </summary>
public sealed class ModbusBatchReadExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	/// <summary>执行 Modbus 批量读取操作</summary>

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusBatchReadPlugin().CreateSerializer();
		var setting = (ModbusBatchReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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

			var results = new List<string>();
			var timeoutMs = ModbusHelper.ResolveTimeoutMs(context, connName);
			foreach (var item in setting.Items)
			{
				cancellationToken.ThrowIfCancellationRequested();

				object result;
				switch (item.RegisterType)
				{
					case ModbusRegisterType.Coil:
						var coils = await ModbusHelper.WithTimeoutAsync(
							master.ReadCoilsAsync(item.SlaveAddress, item.StartAddress, item.Quantity),
							timeoutMs, $"Modbus 批量读取线圈(地址 {item.StartAddress})", cancellationToken);
						result = coils.Length == 1 ? coils[0] : coils;
						break;
					case ModbusRegisterType.DiscreteInput:
						var inputs = await ModbusHelper.WithTimeoutAsync(
							master.ReadInputsAsync(item.SlaveAddress, item.StartAddress, item.Quantity),
							timeoutMs, $"Modbus 批量读取离散输入(地址 {item.StartAddress})", cancellationToken);
						result = inputs.Length == 1 ? inputs[0] : inputs;
						break;
					case ModbusRegisterType.HoldingRegister:
						var holdRegs = await ModbusHelper.WithTimeoutAsync(
							master.ReadHoldingRegistersAsync(item.SlaveAddress, item.StartAddress, item.Quantity),
							timeoutMs, $"Modbus 批量读取保持寄存器(地址 {item.StartAddress})", cancellationToken);
						result = ModbusDataConverter.ConvertRegisters(holdRegs, item.DataFormat);
						break;
					default:
						var inRegs = await ModbusHelper.WithTimeoutAsync(
							master.ReadInputRegistersAsync(item.SlaveAddress, item.StartAddress, item.Quantity),
							timeoutMs, $"Modbus 批量读取输入寄存器(地址 {item.StartAddress})", cancellationToken);
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
		catch (TimeoutException ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = ex.Message }
				}
			};
		}
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = $"Modbus 批量读取失败: {ex.Message}" }
				}
			};
		}
	}
}