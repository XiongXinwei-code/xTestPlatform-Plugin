using LIN.Adapters;
using LIN.Helpers;
using LIN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Executors;

public sealed class LinCloseExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new LinClosePlugin().CreateSerializer();
        var setting = (LinCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = LinHelper.GetAdapterKey(connName);

            if (context.Resources.TryGet<ILinAdapter>(key, out var adapter))
            {
                adapter.Close();
                context.Resources.Remove(key);
                context.LogAction?.Invoke($"LIN 通道已关闭: {connName}");
            }
            else
            {
                context.LogAction?.Invoke($"LIN 通道未找到: {connName}");
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
                    Error  = ErrorInfo.FromException(ex, $"关闭 LIN 通道失败: {ex.Message}")
                }
            };
        }
    }
}
