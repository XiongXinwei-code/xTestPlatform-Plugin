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
/// Modbus 批量写入执行器，逐项写入多个地址段
/// </summary>
public sealed class ModbusBatchWriteExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	/// <summary>执行 Modbus 批量写入操作</summary>

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
        try
        {
			cancellationToken.ThrowIfCancellationRequested();
			var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
			var step = context.CurrentStep!.Step;
			var serializer = new ModbusBatchWritePlugin().CreateSerializer();
			var setting = (ModbusBatchWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
			if (setting.IntervalMs < 0) throw new ArgumentOutOfRangeException(nameof(setting.IntervalMs), "批量写入间隔不能为负数");

			var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
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

			var timeoutMs = ModbusHelper.ResolveTimeoutMs(context, connName);
			var readOnlyItem = setting.Items.FirstOrDefault(i =>
				i.RegisterType is not (ModbusRegisterType.Coil or ModbusRegisterType.HoldingRegister));
			if (readOnlyItem != null)
			{
				return new ExecutionResult
				{
					StepResult = new StepResult
					{
						Status = TestStatus.Error,
						Error = new ErrorInfo { Message = $"地址 {readOnlyItem.StartAddress} 的寄存器类型 {readOnlyItem.RegisterType} 为只读，不支持写入（仅支持 Coil / HoldingRegister）" }
					}
				};
			}

			// 全部值在第一次发送前完成解析，避免后面非法输入导致前面已经输出。
			var prepared = setting.Items.Select(item => (Item: item,
				Coils: item.RegisterType == ModbusRegisterType.Coil ? ModbusDataConverter.ParseCoils(item.Values) : null,
				Registers: item.RegisterType == ModbusRegisterType.HoldingRegister ? ModbusDataConverter.ConvertToRegisters(item.Values, item.DataFormat) : null)).ToArray();
			foreach (var entry in prepared)
			{
				var item = entry.Item;
				epoch.ThrowIfExpired();
				ModbusResultPublisher.EnsureActive(context, key, master, cancellationToken);

				if (item.RegisterType == ModbusRegisterType.Coil)
				{
					var bools = entry.Coils!;
					if (bools.Length == 1)
						await master.WithTimeoutAsync(
							() => { epoch.ThrowIfExpired(); return master.Master.WriteSingleCoilAsync(item.SlaveAddress, item.StartAddress, bools[0]); },
							timeoutMs, $"Modbus 批量写入线圈(地址 {item.StartAddress})", cancellationToken);
					else
						await master.WithTimeoutAsync(
							() => { epoch.ThrowIfExpired(); return master.Master.WriteMultipleCoilsAsync(item.SlaveAddress, item.StartAddress, bools); },
							timeoutMs, $"Modbus 批量写入线圈(地址 {item.StartAddress})", cancellationToken);
				}
				else
				{
					var registers = entry.Registers ?? throw new ArgumentOutOfRangeException(nameof(item.RegisterType));
					if (registers.Length == 1)
						await master.WithTimeoutAsync(
							() => { epoch.ThrowIfExpired(); return master.Master.WriteSingleRegisterAsync(item.SlaveAddress, item.StartAddress, registers[0]); },
							timeoutMs, $"Modbus 批量写入寄存器(地址 {item.StartAddress})", cancellationToken);
					else
						await master.WithTimeoutAsync(
							() => { epoch.ThrowIfExpired(); return master.Master.WriteMultipleRegistersAsync(item.SlaveAddress, item.StartAddress, registers); },
							timeoutMs, $"Modbus 批量写入寄存器(地址 {item.StartAddress})", cancellationToken);
				}

				if (setting.IntervalMs > 0)
					await Task.Delay(setting.IntervalMs, cancellationToken);
			}

			context.Log($"Modbus 批量写入: 已写入 {setting.Items.Count} 项");
			epoch.ThrowIfExpired();
			ModbusResultPublisher.EnsureActive(context, key, master, cancellationToken);

			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Completed, Value = $"已写入 {setting.Items.Count} 项" }
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
					Error = ErrorInfo.FromException(ex, $"Modbus 批量写入超时: {ex.Message}")
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
					Error = ErrorInfo.FromException(ex, $"Modbus 批量写入失败: {ex.Message}")
				}
			};
		}
	}
}
