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
/// Modbus 批量读取执行器，逐项读取多个地址段并将结果存入变量
/// </summary>
public sealed class ModbusBatchReadExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	/// <summary>执行 Modbus 批量读取操作</summary>

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
        try
        {
			cancellationToken.ThrowIfCancellationRequested();
			var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
			var step = context.CurrentStep!.Step;
			var serializer = new ModbusBatchReadPlugin().CreateSerializer();
			var setting = (ModbusBatchReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
			if (setting.IntervalMs < 0) throw new ArgumentOutOfRangeException(nameof(setting.IntervalMs), "批量读取间隔不能为负数");

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

			var results = new List<string>();
			var timeoutMs = ModbusHelper.ResolveTimeoutMs(context, connName);
			// 先验证全部本地配置；真实通信失败仍保留先前已发布结果，不承诺原子回滚。
			foreach (var item in setting.Items)
			{
				ModbusRequestPreflight.ValidateRead(item.RegisterType, item.DataFormat, item.StartAddress, item.Quantity);
				ModbusRequestPreflight.ValidateDestination(context, item.ResultVariable, item.RegisterType, item.DataFormat, item.Quantity, optional: true, epoch.ThrowIfExpired);
				epoch.ThrowIfExpired();
			}
			foreach (var item in setting.Items)
			{
				cancellationToken.ThrowIfCancellationRequested();

				object result;
				switch (item.RegisterType)
				{
					case ModbusRegisterType.Coil:
						var coils = await master.WithTimeoutAsync(
							() => { epoch.ThrowIfExpired(); return master.Master.ReadCoilsAsync(item.SlaveAddress, item.StartAddress, item.Quantity); },
							timeoutMs, $"Modbus 批量读取线圈(地址 {item.StartAddress})", cancellationToken);
						result = coils.Length == 1 ? coils[0] : coils;
						break;
					case ModbusRegisterType.DiscreteInput:
						var inputs = await master.WithTimeoutAsync(
							() => { epoch.ThrowIfExpired(); return master.Master.ReadInputsAsync(item.SlaveAddress, item.StartAddress, item.Quantity); },
							timeoutMs, $"Modbus 批量读取离散输入(地址 {item.StartAddress})", cancellationToken);
						result = inputs.Length == 1 ? inputs[0] : inputs;
						break;
					case ModbusRegisterType.HoldingRegister:
						var holdRegs = await master.WithTimeoutAsync(
							() => { epoch.ThrowIfExpired(); return master.Master.ReadHoldingRegistersAsync(item.SlaveAddress, item.StartAddress, item.Quantity); },
							timeoutMs, $"Modbus 批量读取保持寄存器(地址 {item.StartAddress})", cancellationToken);
						result = ModbusDataConverter.ConvertRegisters(holdRegs, item.DataFormat);
						break;
					case ModbusRegisterType.InputRegister:
						var inRegs = await master.WithTimeoutAsync(
							() => { epoch.ThrowIfExpired(); return master.Master.ReadInputRegistersAsync(item.SlaveAddress, item.StartAddress, item.Quantity); },
							timeoutMs, $"Modbus 批量读取输入寄存器(地址 {item.StartAddress})", cancellationToken);
						result = ModbusDataConverter.ConvertRegisters(inRegs, item.DataFormat);
						break;
					default: throw new ArgumentOutOfRangeException(nameof(item.RegisterType));
				}

				epoch.ThrowIfExpired();
				ModbusResultPublisher.EnsureActive(context, key, master, cancellationToken);
				if (!string.IsNullOrWhiteSpace(item.ResultVariable))
					context.SetVariable(item.ResultVariable, result);

				results.Add($"{item.ResultVariable}={result}");

				if (setting.IntervalMs > 0)
					await Task.Delay(setting.IntervalMs, cancellationToken);
			}

			var summary = string.Join("; ", results);
			context.Log($"Modbus BatchRead: {summary}");
			epoch.ThrowIfExpired();
			ModbusResultPublisher.EnsureActive(context, key, master, cancellationToken);

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Completed, Value = summary }
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
					Error = ErrorInfo.FromException(ex, $"Modbus 批量读取超时: {ex.Message}")
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
					Error = ErrorInfo.FromException(ex, $"Modbus 批量读取失败: {ex.Message}")
				}
			};
		}
	}
}
