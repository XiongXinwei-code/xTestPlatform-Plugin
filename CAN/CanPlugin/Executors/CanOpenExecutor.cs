using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanOpenExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new CanOpenPlugin().CreateSerializer();
        var setting = (CanOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var channel = await Evaluator.EvaluateAsync<string>(setting.Channel, context) ?? setting.Channel;

            var adapter = CanAdapterFactory.Create(setting.AdapterType);
            adapter.Open(new CanAdapterConfig
            {
                Channel = channel,
                BaudRate = setting.BaudRate,
                Protocol = setting.Protocol,
                DataBitRate = setting.DataBitRate
            });

            // 存储到 RuntimeData
            var key = CanHelper.GetAdapterKey(setting.ConnectionName);
            context.CurrentStep.RuntimeData[key] = adapter;

            context.LogAction?.Invoke($"CAN 通道已打开: {channel} ({setting.AdapterType}, {setting.Protocol}, {setting.BaudRate} bps)");

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
                    Error = new ErrorInfo { Message = $"打开 CAN 通道失败: {ex.Message}" }
                }
            };
        }
    }
}
