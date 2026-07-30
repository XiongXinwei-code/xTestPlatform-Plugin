using LXI.Helpers;
using LXI.Models;
using System.Net.Sockets;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LXI.Executors;

public sealed class LxiReadExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			var step = context.CurrentStep!.Step;
			var serializer = new LxiReadPlugin().CreateSerializer();
			var s = (LxiReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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

			var response = await LxiHelper.ReadResponseAsync(client, s.Terminator, s.ReadTimeoutMs, cancellationToken);

			if (!string.IsNullOrWhiteSpace(s.ResultVariable))
				context.SetVariable(s.ResultVariable, response);

			context.LogAction?.Invoke($"LXI {ip} 接收: {response}");

			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Passed,
					Value = response
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