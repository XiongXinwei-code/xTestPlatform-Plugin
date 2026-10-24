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
/// Modbus 写入执行器，支持写入线圈和保持寄存器
/// </summary>
public sealed class ModbusWriteExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
	private readonly Func<string, IExecutionContext, Task<string>> _evaluate;
	public ModbusWriteExecutor() : this((expression, context) => Evaluator.EvalStringAsync(expression, context)) { }
	internal ModbusWriteExecutor(Func<string, IExecutionContext, Task<string>> evaluate) => _evaluate = evaluate;

	/// <summary>执行 Modbus 写入操作</summary>

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
        try
        {
			cancellationToken.ThrowIfCancellationRequested();
			var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
			var step = context.CurrentStep!.Step;
			var serializer = new ModbusWritePlugin().CreateSerializer();
			var setting = (ModbusWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

			var connName = await _evaluate(setting.ConnectionName, context);
			epoch.ThrowIfExpired();
			var key = ModbusHelper.GetConnectionKey(connName);

			if (!context.Resources.TryGet<ModbusConnectionResource>(key, out var master))
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

			var startAddr = ushort.Parse(await _evaluate(setting.StartAddress, context));
			epoch.ThrowIfExpired();
			var valuesStr = await _evaluate(setting.Values, context);
			epoch.ThrowIfExpired();
			var timeoutMs = ModbusHelper.ResolveTimeoutMs(context, connName);

			if (setting.RegisterType is not (ModbusRegisterType.Coil or ModbusRegisterType.HoldingRegister))
			{
				return new ExecutionResult
				{
					StepResult = new StepResult
					{
						Status = TestStatus.Error,
						Error = new ErrorInfo { Message = $"寄存器类型 {setting.RegisterType} 为只读，不支持写入（仅支持 Coil / HoldingRegister）" }
					}
				};
			}

			if (setting.RegisterType == ModbusRegisterType.Coil)
			{
				var bools = ModbusDataConverter.ParseCoils(valuesStr);
				if (bools.Length == 1)
					await master.WithTimeoutAsync(
						() => { epoch.ThrowIfExpired(); return master.Master.WriteSingleCoilAsync(setting.SlaveAddress, startAddr, bools[0]); },
						timeoutMs, "Modbus 写入单个线圈", cancellationToken);
				else
					await master.WithTimeoutAsync(
						() => { epoch.ThrowIfExpired(); return master.Master.WriteMultipleCoilsAsync(setting.SlaveAddress, startAddr, bools); },
						timeoutMs, "Modbus 写入多个线圈", cancellationToken);
			}
			else
			{
				var registers = ModbusDataConverter.ConvertToRegisters(valuesStr, setting.DataFormat);
				if (registers.Length == 1)
					await master.WithTimeoutAsync(
						() => { epoch.ThrowIfExpired(); return master.Master.WriteSingleRegisterAsync(setting.SlaveAddress, startAddr, registers[0]); },
						timeoutMs, "Modbus 写入单个寄存器", cancellationToken);
				else
					await master.WithTimeoutAsync(
						() => { epoch.ThrowIfExpired(); return master.Master.WriteMultipleRegistersAsync(setting.SlaveAddress, startAddr, registers); },
						timeoutMs, "Modbus 写入多个寄存器", cancellationToken);
			}

			context.Log($"Modbus Write: Addr={startAddr}, Values={valuesStr}");
			epoch.ThrowIfExpired();
			ModbusResultPublisher.EnsureActive(context, key, master, cancellationToken);

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Completed, Value = $"已写入: Addr={startAddr}" }
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
					Error = ErrorInfo.FromException(ex, $"Modbus 写入超时: {ex.Message}")
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
					Error = ErrorInfo.FromException(ex, $"Modbus 写入失败: {ex.Message}")
				}
			};
		}
	}
}
