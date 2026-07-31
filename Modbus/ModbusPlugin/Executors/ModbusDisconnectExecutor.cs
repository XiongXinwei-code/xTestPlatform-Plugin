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

public sealed class ModbusDisconnectExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		var step = context.CurrentStep!.Step;
		var serializer = new ModbusDisconnectPlugin().CreateSerializer();
		var setting = (ModbusDisconnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

		try
		{
			var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
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

			context.LogAction?.Invoke($"Modbus 杩炴帴宸插叧闂? {connName}");
			return new ExecutionResult
			{
				StepResult = new StepResult { Status = TestStatus.Passed, Value = $"宸叉柇寮€: {connName}" }
			};
		}
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = $"Modbus 鏂紑澶辫触: {ex.Message}" }
				}
			};
		}
	}
}
