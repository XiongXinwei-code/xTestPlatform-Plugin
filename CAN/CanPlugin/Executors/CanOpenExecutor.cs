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
            var channel = await Evaluator.EvalStringAsync(setting.Channel, context);
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);

            // 若已存在同名连接（序列异常终止未关闭），先关闭销毁旧适配器
            var key = CanHelper.GetAdapterKey(connName);
            if (context.CurrentStep.RuntimeData.TryGetValue(key, out var existingAdapter) && existingAdapter is ICanAdapter oldAdapter)
            {
                try { oldAdapter.Close(); oldAdapter.Dispose(); } catch { /* 忽略关闭异常 */ }
                context.LogAction?.Invoke($"CAN 连接 {connName} 检测到已有连接，已自动关闭旧连接");
            }

            var adapter = CanAdapterFactory.Create(setting.AdapterType);
            adapter.Open(new CanAdapterConfig
            {
                Channel = channel,
                BaudRate = setting.BaudRate,
                Protocol = setting.Protocol,
                DataBitRate = setting.DataBitRate
            });

            // 存储到 RuntimeData
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
