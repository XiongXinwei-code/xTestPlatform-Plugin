using LIN.Adapters;
using LIN.Helpers;
using LIN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Executors;

public sealed class LinOpenExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new LinOpenPlugin().CreateSerializer();
        var setting = (LinOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var channel  = await Evaluator.EvalStringAsync(setting.Channel, context);
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);

            var key = LinHelper.GetAdapterKey(connName);

            var adapter = LinAdapterFactory.Create(setting.AdapterType);
            adapter.Open(new LinAdapterConfig
            {
                Channel    = channel,
                BaudRate   = setting.BaudRate,
                LinVersion = setting.LinVersion,
                IsMaster   = setting.IsMaster,
                RxQueueSize = setting.RxQueueSize
            });

            // Set 会自动销毁同名旧适配器（如上次运行异常终止未关闭）
            context.Resources.Set(key, adapter);
            context.LogAction?.Invoke($"LIN 通道已打开: {channel} ({setting.AdapterType}, LIN {setting.LinVersion}, {setting.BaudRate} bps, {(setting.IsMaster ? "主节点" : "从节点")})");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed }
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Aborted }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error  = new ErrorInfo { Message = $"打开 LIN 通道失败: {ex.Message}" }
                }
            };
        }
    }
}
