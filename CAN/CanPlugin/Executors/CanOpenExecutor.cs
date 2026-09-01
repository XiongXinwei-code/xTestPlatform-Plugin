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

            // 用户只配置厂商无关的 Nominal/Data 采样点百分比。NI-XNET 需要分别预先编码为 U64，
            // 其他适配器在各自 Open 实现内转换为对应的位时序表示。
            var arbitrationTiming = setting.AdapterType == CanAdapterType.NI
                ? CanBitTimingCalculator.Calculate(setting.BaudRate, setting.ArbitrationSamplePoint)
                : null;
            var dataTiming = setting.AdapterType == CanAdapterType.NI && setting.Protocol == CanProtocolType.FD
                ? CanBitTimingCalculator.CalculateData(setting.DataBitRate, setting.DataSamplePoint)
                : null;

            var adapter = CanAdapterFactory.Create(setting.AdapterType);
            var adapterConfig = new CanAdapterConfig
            {
                Channel = channel,
                BaudRate = setting.BaudRate,
                Protocol = setting.Protocol,
                DataBitRate = setting.DataBitRate,
                EnableTermination = setting.EnableTermination,
                ArbitrationSamplePoint = setting.ArbitrationSamplePoint,
                DataSamplePoint = setting.DataSamplePoint,
                ArbitrationBitTiming = arbitrationTiming,
                DataBitTiming = dataTiming,
                RxQueueSize = setting.RxQueueSize
            };
            adapter.Open(adapterConfig);

            // Set 会自动销毁同名旧适配器（如上次运行异常终止未关闭）
            context.Resources.Set(key, adapter);

            string timingDetails;
            if (arbitrationTiming != null)
            {
                timingDetails = $"仲裁段: {CanBitTimingCalculator.Describe(arbitrationTiming)}";
            }
            else if (adapterConfig.AppliedArbitrationBitRate.HasValue &&
                     adapterConfig.AppliedArbitrationSamplePoint.HasValue)
            {
                timingDetails =
                    $"仲裁段={adapterConfig.AppliedArbitrationBitRate:F0} bps, " +
                    $"目标采样点={setting.ArbitrationSamplePoint:F2}%, " +
                    $"实际采样点={adapterConfig.AppliedArbitrationSamplePoint:F2}%";
            }
            else
            {
                timingDetails =
                    $"仲裁段={setting.BaudRate} bps, 目标采样点={setting.ArbitrationSamplePoint:F2}%";
            }
            if (setting.Protocol == CanProtocolType.FD)
            {
                string dataDetails = dataTiming != null
                    ? $"数据段: {CanBitTimingCalculator.Describe(dataTiming)}"
                    : adapterConfig.AppliedDataBitRate.HasValue && adapterConfig.AppliedDataSamplePoint.HasValue
                        ? $"数据段={adapterConfig.AppliedDataBitRate:F0} bps, 目标采样点={setting.DataSamplePoint:F2}%, 实际采样点={adapterConfig.AppliedDataSamplePoint:F2}%"
                        : $"数据段={setting.DataBitRate} bps, 目标采样点={setting.DataSamplePoint:F2}%";
                timingDetails += $"; {dataDetails}";
            }
            string terminationDetails = $"内置终端电阻={(setting.EnableTermination ? "已使能" : "未使能")}";
            context.LogAction?.Invoke(
                $"CAN 通道已打开: {channel} ({setting.AdapterType}, {setting.Protocol}); " +
                $"{timingDetails}; {terminationDetails}");

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
