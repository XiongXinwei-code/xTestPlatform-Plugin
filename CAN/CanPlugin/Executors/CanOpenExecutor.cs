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

            var key = CanHelper.GetAdapterKey(connName);

            // 位时序与软件终端电阻是 NI-XNET 接口能力。切换到其他厂商时保留序列中
            // 的 NI 参数但不应用，便于同一序列在不同适配器之间切换。
            var arbitrationTiming = setting.AdapterType == CanAdapterType.NI
                ? CanBitTimingCalculator.Resolve(setting)
                : null;
            bool enableTermination = setting.AdapterType == CanAdapterType.NI && setting.EnableTermination;

            var adapter = CanAdapterFactory.Create(setting.AdapterType);
            adapter.Open(new CanAdapterConfig
            {
                Channel = channel,
                BaudRate = setting.BaudRate,
                Protocol = setting.Protocol,
                DataBitRate = setting.DataBitRate,
                EnableTermination = enableTermination,
                ArbitrationBitTiming = arbitrationTiming,
                RxQueueSize = setting.RxQueueSize
            });

            // Set 会自动销毁同名旧适配器（如上次运行异常终止未关闭）
            context.Resources.Set(key, adapter);

            string timingDetails = arbitrationTiming == null
                ? $"仲裁段={setting.BaudRate} bps（驱动默认采样点）"
                : $"仲裁段自定义: {CanBitTimingCalculator.Describe(arbitrationTiming)}";
            string terminationDetails = setting.AdapterType == CanAdapterType.NI
                ? $"内置终端电阻={(enableTermination ? "已使能" : "未使能")}" : "";
            context.LogAction?.Invoke(
                $"CAN 通道已打开: {channel} ({setting.AdapterType}, {setting.Protocol}); " +
                $"{timingDetails}{(terminationDetails.Length > 0 ? $"; {terminationDetails}" : "")}");

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
                    Error = ErrorInfo.FromException(ex, $"打开 CAN 通道失败: {ex.Message}")
                }
            };
        }
    }
}
