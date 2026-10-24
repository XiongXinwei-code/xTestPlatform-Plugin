using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqDoWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Action<DaqTaskResource, string>? _configure;
    private readonly Action<DaqTaskResource, bool, bool, uint>? _write;
    public NiDaqDoWriteExecutor() { }
    internal NiDaqDoWriteExecutor(Action<DaqTaskResource, string> configure, Action<DaqTaskResource, bool, bool, uint> write) { _configure = configure; _write = write; }

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new DaqExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new NiDaqDoWritePlugin().CreateSerializer();
            var setting = (NiDaqDoWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var channel = await Evaluator.EvalStringAsync(setting.Channel, context);
            var valueStr = await Evaluator.EvalStringAsync(setting.Value, context);
            guard.Check();

            bool isBit = bool.TryParse(valueStr, out bool bit);
            uint numeric = isBit ? 0 : uint.Parse(valueStr.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
            long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            using var resource = new DaqTaskResource(ex => context.Log(LogLevel.Error, "DAQ DO 收尾未确认", ex.ToString()), [channel]);
            resource.ExecutionCheck = guard.Check;
            resource.RegisterTransient(context.Resources, guard.Check);
            await resource.ConfigureAsync(() => { if (_configure != null) { _configure(resource, channel); return; } var task = resource.AddTask(); resource.EnsureOperationActive(); task.DOChannels.CreateChannel(channel, "", ChannelLineGrouping.OneChannelForAllLines); }, cancellationToken, DaqTaskFactory.RemainingBudget(startedAt, setting.TimeoutMs));
            await resource.RunAsync(() =>
            {
                if (_write != null) { _write(resource, isBit, bit, numeric); return true; }
                var task = resource.Primary;
                long width = task.DOChannels[0].NumberOfLines;
                if (width <= 0 || width > 32 || isBit && width != 1) throw new ArgumentException("bool 输出必须对应单条数字线，端口整数输出最多 32 位");
                if (!isBit && width < 32 && numeric >= (1u << (int)width)) throw new ArgumentOutOfRangeException(nameof(numeric), "输出值超出所选数字线位宽，拒绝截断");
                task.Stream.Timeout = DaqTaskFactory.RemainingBudget(startedAt, setting.TimeoutMs);
                var writer = new DigitalSingleChannelWriter(task.Stream);
                resource.EnsureOperationActive();
                if (isBit) writer.WriteSingleSampleSingleLine(true, bit); else writer.WriteSingleSamplePort(true, numeric);
                return true;
            }, DaqTaskFactory.RemainingBudget(startedAt, setting.TimeoutMs), "DO 输出", cancellationToken);
            await resource.CloseAsync(DaqTaskFactory.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken);
            guard.Check();

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = valueStr
                }
            };
        }
        catch (TimeoutException ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex)
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
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
                    Error = ErrorInfo.FromException(ex, $"DO 输出失败：{ex.Message}")
                }
            };
        }
    }
}
