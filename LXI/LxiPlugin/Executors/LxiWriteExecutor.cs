using LXI.Helpers;
using LXI.Models;
using System.Net.Sockets;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LXI.Executors;

public sealed class LxiWriteExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			var step = context.CurrentStep!.Step;
			var serializer = new LxiWritePlugin().CreateSerializer();
			var s = (LxiWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

			var ip = await Evaluator.EvaluateAsync<string>(s.IpAddress, context) ?? string.Empty;

			if (string.IsNullOrWhiteSpace(ip))
				return new ExecutionResult
				{
					StepResult = new StepResult
					{
						Status = TestStatus.Error,
						Error = new ErrorInfo { Message = "IP 地址为空" }
					}
				};

			var key = LxiHelper.GetConnectionKey(ip);

			if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not TcpClient client || !client.Connected)
				return new ExecutionResult
				{
					StepResult = new StepResult
					{
						Status = TestStatus.Error,
						Error = new ErrorInfo { Message = $"LXI 连接 {ip} 未打开，请先执行 LXI_Open" }
					}
				};

			var command = await Evaluator.EvaluateAsync<string>(s.Command, context) ?? string.Empty;

			await LxiHelper.WriteCommandAsync(client, command, s.Terminator, cancellationToken);

			context.LogAction?.Invoke($"LXI {ip} 发送: {command}");

			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Passed,
					Value = command
				}
			};
		}
		catch (OperationCanceledException)
		{
			return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
		}
		catch (Exception ex)
		{
			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Error,
					Error = new ErrorInfo { Message = ex.Message }
				}
			};
		}
	}
}