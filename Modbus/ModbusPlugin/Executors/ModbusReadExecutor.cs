using PluginCommon;
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
        try
        {
			cancellationToken.ThrowIfCancellationRequested();
			var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
			var step = context.CurrentStep!.Step;
			var serializer = new ModbusReadPlugin().CreateSerializer();
			var setting = (ModbusReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

			var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
			epoch.ThrowIfExpired();
			var key = ModbusHelper.GetConnectionKey(connName);

			if (!context.Resources.TryGet<ModbusConnectionResource>(key, out var master))
			{
				epoch.ThrowIfExpired();
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
			epoch.ThrowIfExpired();
			var quantity = ushort.Parse(await Evaluator.EvalStringAsync(setting.Quantity, context));
			epoch.ThrowIfExpired();
			var timeoutMs = ModbusHelper.ResolveTimeoutMs(context, connName);
			ModbusRequestPreflight.ValidateRead(setting.RegisterType, setting.DataFormat, startAddr, quantity);
			ModbusRequestPreflight.ValidateDestination(context, setting.ResultVariable, setting.RegisterType, setting.DataFormat, quantity, optional: false, epoch.ThrowIfExpired);
			epoch.ThrowIfExpired();

			object result;
			switch (setting.RegisterType)
			{
				case ModbusRegisterType.Coil:
					var coils = await master.WithTimeoutAsync(
						() => { epoch.ThrowIfExpired(); return master.Master.ReadCoilsAsync(setting.SlaveAddress, startAddr, quantity); },
						timeoutMs, "Modbus 读取线圈", cancellationToken);
					result = coils.Length == 1 ? coils[0] : coils;
					break;
				case ModbusRegisterType.DiscreteInput:
					var inputs = await master.WithTimeoutAsync(
						() => { epoch.ThrowIfExpired(); return master.Master.ReadInputsAsync(setting.SlaveAddress, startAddr, quantity); },
						timeoutMs, "Modbus 读取离散输入", cancellationToken);
					result = inputs.Length == 1 ? inputs[0] : inputs;
					break;
				case ModbusRegisterType.HoldingRegister:
					var holdRegs = await master.WithTimeoutAsync(
						() => { epoch.ThrowIfExpired(); return master.Master.ReadHoldingRegistersAsync(setting.SlaveAddress, startAddr, quantity); },
						timeoutMs, "Modbus 读取保持寄存器", cancellationToken);
					result = ModbusDataConverter.ConvertRegisters(holdRegs, setting.DataFormat);
					break;
				case ModbusRegisterType.InputRegister:
					var inRegs = await master.WithTimeoutAsync(
						() => { epoch.ThrowIfExpired(); return master.Master.ReadInputRegistersAsync(setting.SlaveAddress, startAddr, quantity); },
						timeoutMs, "Modbus 读取输入寄存器", cancellationToken);
					result = ModbusDataConverter.ConvertRegisters(inRegs, setting.DataFormat);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(setting.RegisterType));
			}

			var varName = setting.ResultVariable;
			epoch.ThrowIfExpired();
			ModbusResultPublisher.EnsureActive(context, key, master, cancellationToken);
			context.SetVariable(varName, result);

			context.Log($"Modbus Read: Addr={startAddr}, Qty={quantity}, Result={result}");
			epoch.ThrowIfExpired();
			ModbusResultPublisher.EnsureActive(context, key, master, cancellationToken);

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Completed, Value = result.ToString() ?? string.Empty }
			};
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
					Error = ErrorInfo.FromException(ex, $"Modbus 读取超时: {ex.Message}")
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
