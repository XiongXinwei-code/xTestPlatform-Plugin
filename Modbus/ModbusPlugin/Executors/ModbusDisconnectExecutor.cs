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
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusDisconnectPlugin().CreateSerializer();
		var setting = (ModbusDisconnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

		try
		{
			var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
			var key = ModbusHelper.GetConnectionKey(connName);

			if (context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) && obj is IModbusMaster master)
			{
				master.Dispose();
				context.CurrentStep.RuntimeData.Remove(key);
			}

			if (context.CurrentStep.RuntimeData.TryGetValue(key + "_transport", out var tObj))
			{
				if (tObj is TcpClient tcp) tcp.Dispose();
				else if (tObj is SerialPort sp) { sp.Close(); sp.Dispose(); }
				context.CurrentStep.RuntimeData.Remove(key + "_transport");
			}

			context.LogAction?.Invoke($"Modbus 连接已关闭: {connName}");
			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已断开: {connName}" }
			};
		}
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = $"Modbus 断开失败: {ex.Message}" }
				}
			};
		}
	}
}