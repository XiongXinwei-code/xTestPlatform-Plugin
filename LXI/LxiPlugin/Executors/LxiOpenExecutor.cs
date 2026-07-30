using LXI.Helpers;
using LXI.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LXI.Executors;

public sealed class LxiOpenExecutor : IStepExecutor
{
	private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

	public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			var step = context.CurrentStep!.Step;
			var serializer = new LxiOpenPlugin().CreateSerializer();
			var s = (LxiOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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

			var client = await LxiHelper.ConnectAsync(ip, s.Port, s.ConnectTimeoutMs, cancellationToken);

			var key = LxiHelper.GetConnectionKey(ip);
			context.CurrentStep.RuntimeData[key] = client;

			context.LogAction?.Invoke($"LXI 已连接 {ip}:{s.Port}");

			return new ExecutionResult
			{
				StepResult = new StepResult
				{
					Status = TestStatus.Passed,
					Value = $"{ip}:{s.Port}"
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