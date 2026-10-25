using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using System.Diagnostics;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanOpenExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Func<CanAdapterType, ICanAdapter> _create;
    public CanOpenExecutor() : this(CanAdapterFactory.Create) { }
    internal CanOpenExecutor(Func<CanAdapterType, ICanAdapter> create) => _create = create;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = PluginCommon.ExecutionEpoch.Capture(context.Resources, "CAN");
            var step = context.CurrentStep!.Step;
            var serializer = new CanOpenPlugin().CreateSerializer();
            var setting = (CanOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.TimeoutMs);
            var channel = await Evaluator.EvalStringAsync(setting.Channel, context);
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);

            var budget = Stopwatch.StartNew();
            int Remaining()
            {
                int remaining = setting.TimeoutMs - (int)Math.Min(int.MaxValue, budget.ElapsedMilliseconds);
                if (remaining <= 0) throw new TimeoutException($"CAN 打开总期限 {setting.TimeoutMs} ms 已到（含登记、旧连接收尾与本次驱动打开）");
                return remaining;
            }
            var key = CanHelper.GetAdapterKey(connName);
            using var lifecycle = await CanRegistryGuard.EnterAsync(context.Resources, connName, cancellationToken, Remaining());
            epoch.ThrowIfExpired();
            if (context.Resources.TryGet<ICanAdapter>(key, out var previous))
            {
                if (previous is not CanConnectionResource retiring || (!retiring.IsClosing && !retiring.IsClosed))
                    throw new InvalidOperationException($"CAN 连接 {connName} 已存在，请先成功关闭原连接");
                try { await retiring.CloseAsync(Remaining(), cancellationToken); }
                catch (Exception ex) when (retiring.IsClosed && !cancellationToken.IsCancellationRequested)
                {
                    context.Log(LogLevel.Error, "旧 CAN 连接已确认释放；保留历史收尾错误，本次继续打开", ex.ToString());
                }
                finally
                {
                    if (retiring.IsClosed && context.Resources.TryGet<ICanAdapter>(key, out var current) && ReferenceEquals(current, retiring))
                        context.Resources.Remove(key);
                }
            }

            // 用户只配置厂商无关的 Nominal/Data 采样点百分比。NI-XNET 需要分别预先编码为 U64，
            // 其他适配器在各自 Open 实现内转换为对应的位时序表示。
            var arbitrationTiming = setting.AdapterType == CanAdapterType.NI
                ? CanBitTimingCalculator.Calculate(setting.BaudRate, setting.ArbitrationSamplePoint)
                : null;
            var dataTiming = setting.AdapterType == CanAdapterType.NI && setting.Protocol == CanProtocolType.FD
                ? CanBitTimingCalculator.CalculateData(setting.DataBitRate, setting.DataSamplePoint)
                : null;

            var physicalKey = CanChannelIdentity.Normalize(setting.AdapterType, channel);
            await CanConnectionResource.WaitForPreviousOwnerAsync(physicalKey, Remaining(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            epoch.ThrowIfExpired();
            var raw = _create(setting.AdapterType);
            CanConnectionResource adapter;
            try { adapter = new CanConnectionResource(raw, physicalKey,
                ex => context.Log(LogLevel.Error, "CAN 自动收尾未确认，请查看详情", ex.ToString())); }
            catch { raw.Dispose(); throw; } // No Open has been submitted for this losing reservation.
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
            // 打开可能已取得部分原生句柄；先登记所有者，清理失败后仍可通过 CAN_Close 重试。
            try
            {
                epoch.ThrowIfExpired();
                if (!context.Resources.TryAdd(key, adapter, ResourceLifetime.Execution))
                    throw new InvalidOperationException($"CAN 连接 {connName} 已被其他步骤注册");
                epoch.ThrowIfExpired();
                await adapter.OpenAsync(adapterConfig, Remaining(), cancellationToken, epoch.ThrowIfExpired);
                epoch.ThrowIfExpired();
                _ = Remaining();
            }
            catch
            {
                adapter.Dispose();
                if (adapter.IsClosed && context.Resources.TryGet<ICanAdapter>(key, out var current) && ReferenceEquals(current, adapter)) context.Resources.Remove(key);
                throw;
            }

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
            else if (setting.AdapterType == CanAdapterType.ZLG &&
                     adapterConfig.AppliedArbitrationBitRate.HasValue)
            {
                timingDetails =
                    $"仲裁段={adapterConfig.AppliedArbitrationBitRate:F0} bps, 使用驱动标准波特率；" +
                    $"采样点由驱动决定，未应用目标采样点 {setting.ArbitrationSamplePoint:F2}%";
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
            try
            {
                context.Log(
                    $"CAN 通道已打开: {channel} ({setting.AdapterType}, {setting.Protocol}); " +
                    $"{timingDetails}; {terminationDetails}");
                cancellationToken.ThrowIfCancellationRequested();
                epoch.ThrowIfExpired();
                _ = Remaining();
            }
            catch { adapter.Dispose(); throw; }

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
