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
		var step = context.CurrentStep!.Step;
		var serializer = new SerialPortQueryPlugin().CreateSerializer();
		var s = (SerialPortQuerySetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

		try
		{
			var portName = await Evaluator.EvalStringAsync(s.PortName, context);

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

			if (!context.Resources.TryGet<SysSerialPort>(key, out var port) || !port.IsOpen)
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
			var writeBytes = SerialPortHelper.ConvertToBytes(writeData, s.DataFormat);
			await SerialPortHelper.WriteWithTimeoutAsync(port, writeBytes, port.WriteTimeout, cancellationToken);

			// Read
			var buffer = await SerialPortHelper.ReadFrameAsync(
				port, s.ReadBytes, s.Terminator, s.ReadTimeoutMs, "串口查询读取", cancellationToken);

			var result = SerialPortHelper.ConvertFromBytes(buffer, s.DataFormat);

			if (!string.IsNullOrWhiteSpace(s.ResultVariable))
				context.SetVariable(s.ResultVariable, result);

			context.Log($"串口 {portName} 查询: 发送 {writeBytes.Length} 字节，接收 {buffer.Length} 字节 ({s.DataFormat})");

			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Passed,
					Value = result
				}
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
					Error = ErrorInfo.FromException(ex)
				}
			};
		}
	}
}