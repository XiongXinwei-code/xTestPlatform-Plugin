using PluginCommon;
using SerialPort.Helpers;
using SerialPort.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using SysSerialPort = System.IO.Ports.SerialPort;

namespace SerialPort.Executors;

public sealed class SerialPortQueryExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
			var step = context.CurrentStep?.Step ?? throw new InvalidOperationException("串口查询步骤上下文不存在");
			var serializer = new SerialPortQueryPlugin().CreateSerializer();
			var s = (SerialPortQuerySetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
			var portName = await Evaluator.EvalStringAsync(s.PortName, context);
			epoch.ThrowIfExpired();

			if (string.IsNullOrWhiteSpace(portName))
				return new ExecutionResult
				{
					StepResult = new StepResult
					{
						Status = TestStatus.Error,
						Error = new ErrorInfo { Message = "端口名称为空" }
					}
				};

			var key = SerialPortHelper.GetPortKey(portName);

			if (!context.Resources.TryGet<SerialConnectionResource>(key, out var port) || !port.IsOpen)
				return new ExecutionResult
				{
					StepResult = new StepResult
					{
						Status = TestStatus.Error,
						Error = new ErrorInfo { Message = $"串口 {portName} 未打开，请先执行 SerialPort_Open" }
					}
				};

			// Write
			var writeData = await Evaluator.EvalStringAsync(s.WriteData, context);
			epoch.ThrowIfExpired();
			if (writeData.Length == 0) throw new ArgumentException("串口查询发送数据求值结果不能为空");
			var writeBytes = SerialPortHelper.ConvertToBytes(writeData, s.DataFormat);
			epoch.ThrowIfExpired();
			var buffer = await port.QueryAsync(writeBytes, s.ReadBytes, s.Terminator, s.ReadTimeoutMs, cancellationToken, epoch.ThrowIfExpired);

			var result = SerialPortHelper.ConvertFromBytes(buffer, s.DataFormat);

			epoch.ThrowIfExpired();
			SerialResultPublisher.EnsureActive(context, key, port, cancellationToken);
			if (!string.IsNullOrWhiteSpace(s.ResultVariable))
				context.SetVariable(s.ResultVariable, result);

			context.Log($"串口 {portName} 查询: 发送 {writeBytes.Length} 字节，接收 {buffer.Length} 字节 ({s.DataFormat})");
			epoch.ThrowIfExpired();
			SerialResultPublisher.EnsureActive(context, key, port, cancellationToken);

			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Completed,
					Value = result
				}
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
					Error = ErrorInfo.FromException(ex, "串口查询超时")
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
					Error = ErrorInfo.FromException(ex, "串口查询失败（含步骤设置解析）")
				}
			};
		}
	}
}
