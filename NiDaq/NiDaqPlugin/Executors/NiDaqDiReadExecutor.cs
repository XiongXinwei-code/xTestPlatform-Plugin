using NationalInstruments.DAQmx;
using DaqTask = NationalInstruments.DAQmx.Task;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using NiDaq.Helpers;

namespace NiDaq.Executors;

public sealed class NiDaqDiReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Action<DaqTaskResource, string>? _configure;
    private readonly Func<DaqTaskResource, uint>? _read;
    public NiDaqDiReadExecutor() { }
    internal NiDaqDiReadExecutor(Action<DaqTaskResource, string> configure, Func<DaqTaskResource, uint> read) { _configure = configure; _read = read; }

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new DaqExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new NiDaqDiReadPlugin().CreateSerializer();
            var setting = (NiDaqDiReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var channel = await Evaluator.EvalStringAsync(setting.Channel, context);
            guard.Check();
            var resultVar = setting.ResultVariable;

            long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            using var resource = new DaqTaskResource(ex => context.Log(LogLevel.Error, "DAQ DI 收尾未确认", ex.ToString()), [channel]);
            resource.ExecutionCheck = guard.Check;
            resource.RegisterTransient(context.Resources, guard.Check);
            await resource.ConfigureAsync(() => { if (_configure != null) { _configure(resource, channel); return; } var task = resource.AddTask(); resource.EnsureOperationActive(); task.DIChannels.CreateChannel(channel, "", ChannelLineGrouping.OneChannelForAllLines); }, cancellationToken, DaqTaskFactory.RemainingBudget(startedAt, setting.TimeoutMs));
            uint data = await resource.RunAsync(() =>
            {
                if (_read != null) return _read(resource);
                var task = resource.Primary;
                if (task.DIChannels[0].NumberOfLines > 32) throw new NotSupportedException("DI 端口超过 32 位，当前 uint 结果不能完整表达");
                task.Stream.Timeout = DaqTaskFactory.RemainingBudget(startedAt, setting.TimeoutMs);
                resource.EnsureOperationActive();
                return new DigitalSingleChannelReader(task.Stream).ReadSingleSamplePortUInt32();
            }, DaqTaskFactory.RemainingBudget(startedAt, setting.TimeoutMs), "DI 读取", cancellationToken);
            await resource.CloseAsync(DaqTaskFactory.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken);

            guard.Check();
            DaqResultPublisher.Publish(context, resultVar, data, cancellationToken);
            guard.Check();

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = data.ToString()
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
                    Error = ErrorInfo.FromException(ex, $"DI 读取失败：{ex.Message}")
                }
            };
        }
    }
}
