using LXI.Helpers;
using LXI.Models;
using System.Net.Sockets;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LXI.Executors;

public sealed class LxiCloseExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			var step = context.CurrentStep!.Step;
			var serializer = new LxiClosePlugin().CreateSerializer();
			var s = (LxiCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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

			if (context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) && obj is TcpClient client)
			{
				client.Close();
				context.CurrentStep.RuntimeData.Remove(key);
				context.LogAction?.Invoke($"LXI 已断开 {ip}");
			}

			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Passed,
					Value = ip
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