using PluginCommon;
using System.IO.Ports;
using System.Net.Sockets;
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.Executors;

/// <summary>
/// Modbus 断开连接执行器，关闭并释放 Master 和传输层资源
/// </summary>
public sealed class ModbusDisconnectExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	/// <summary>执行 Modbus 断开连接操作</summary>

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
        try
        {
			cancellationToken.ThrowIfCancellationRequested();
			var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
			var step = context.CurrentStep!.Step;
			var serializer = new ModbusDisconnectPlugin().CreateSerializer();
			var setting = (ModbusDisconnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

			if (setting.TimeoutMs < 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs));
			var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
			epoch.ThrowIfExpired();
			if (string.IsNullOrWhiteSpace(connName)) throw new ArgumentException("Modbus 连接名称不能为空");
			cancellationToken.ThrowIfCancellationRequested();
			var key = ModbusHelper.GetConnectionKey(connName);

			ModbusConnectionResource? resource;
			lock (ModbusHelper.RegistryLock(context.Resources))
			{
				context.Resources.TryGet<ModbusConnectionResource>(key, out resource);
				epoch.ThrowIfExpired();
				ModbusHelper.MarkCloseRequested(context.Resources, key);
			}
			if (resource != null)
			{
				epoch.ThrowIfExpired();
				await resource.CloseAsync(setting.TimeoutMs > 0 ? setting.TimeoutMs : resource.TimeoutMs, cancellationToken);
				epoch.ThrowIfExpired();
				lock (ModbusHelper.RegistryLock(context.Resources))
				{
					if (context.Resources.TryGet<ModbusConnectionResource>(key, out var current) && ReferenceEquals(current, resource))
					context.Resources.Remove(key);
				}
			}

			context.Log($"Modbus 连接已关闭: {connName}");
			epoch.ThrowIfExpired();
			ModbusResultPublisher.EnsureCallerActive(context, cancellationToken);
			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Completed, Value = $"已断开: {connName}" }
			};
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{ return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } }; }
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = ErrorInfo.FromException(ex, $"Modbus 断开失败: {ex.Message}")
				}
			};
		}
	}
}
