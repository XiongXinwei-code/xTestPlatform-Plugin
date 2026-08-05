using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanCloseExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new CanClosePlugin().CreateSerializer();
        var setting = (CanCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
            var key = CanHelper.GetAdapterKey(connName);

            if (context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) && obj is ICanAdapter adapter)
            {
                adapter.Close();
                adapter.Dispose();
                context.CurrentStep.RuntimeData.Remove(key);
                context.LogAction?.Invoke($"CAN 通道已关闭: {connName}");
            }
            else
            {
                context.LogAction?.Invoke($"CAN 通道未找到: {connName}");
            }

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"关闭 CAN 通道失败: {ex.Message}" }
                }
            };
        }
    }
}
