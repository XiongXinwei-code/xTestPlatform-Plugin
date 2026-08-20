using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

/// <summary>
/// Modbus 读取执行器，支持读取线圈、离散输入、保持寄存器、输入寄存器
/// </summary>
public sealed class ModbusReadExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	/// <summary>执行 Modbus 读取操作，读取结果存入指定变量</summary>

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusReadPlugin().CreateSerializer();
		var setting = (ModbusReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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

			var startAddr = ushort.Parse(await Evaluator.EvalStringAsync(setting.StartAddress, context));
			var quantity = ushort.Parse(await Evaluator.EvalStringAsync(setting.Quantity, context));
			var timeoutMs = ModbusHelper.ResolveTimeoutMs(context, connName);

			object result;
			switch (setting.RegisterType)
			{
				case ModbusRegisterType.Coil:
					var coils = await ModbusHelper.WithTimeoutAsync(
						master.ReadCoilsAsync(setting.SlaveAddress, startAddr, quantity),
						timeoutMs, "Modbus 读取线圈", cancellationToken);
					result = coils.Length == 1 ? coils[0] : coils;
					break;
				case ModbusRegisterType.DiscreteInput:
					var inputs = await ModbusHelper.WithTimeoutAsync(
						master.ReadInputsAsync(setting.SlaveAddress, startAddr, quantity),
						timeoutMs, "Modbus 读取离散输入", cancellationToken);
					result = inputs.Length == 1 ? inputs[0] : inputs;
					break;
				case ModbusRegisterType.HoldingRegister:
					var holdRegs = await ModbusHelper.WithTimeoutAsync(
						master.ReadHoldingRegistersAsync(setting.SlaveAddress, startAddr, quantity),
						timeoutMs, "Modbus 读取保持寄存器", cancellationToken);
					result = ModbusDataConverter.ConvertRegisters(holdRegs, setting.DataFormat);
					break;
				case ModbusRegisterType.InputRegister:
					var inRegs = await ModbusHelper.WithTimeoutAsync(
						master.ReadInputRegistersAsync(setting.SlaveAddress, startAddr, quantity),
						timeoutMs, "Modbus 读取输入寄存器", cancellationToken);
					result = ModbusDataConverter.ConvertRegisters(inRegs, setting.DataFormat);
					break;
				default:
					result = "Unknown register type";
					break;
			}

			var varName = setting.ResultVariable;
			context.SetVariable(varName, result);

			context.LogAction?.Invoke($"Modbus Read: Addr={startAddr}, Qty={quantity}, Result={result}");

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = result?.ToString() }
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
					Error = ErrorInfo.FromException(ex)
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
					Error = ErrorInfo.FromException(ex, $"Modbus 读取失败: {ex.Message}")
				}
			};
		}
	}
}