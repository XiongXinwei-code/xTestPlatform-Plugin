using System.Diagnostics;
using System.Globalization;
using NiDaq.Helpers;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace NiDaq.Executors;

public sealed class NiDaqAoWriteSingleExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new DaqExecutionGuard(context, cancellationToken);
            long startedAt = Stopwatch.GetTimestamp();
            var step = context.CurrentStep?.Step ?? throw new InvalidOperationException("AO 写入缺少当前步骤上下文");
            guard.Check();
            var s = (NiDaqAoWriteSingleSetting)new NiDaqAoWriteSinglePlugin().CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            int Remaining() => DaqTaskFactory.RemainingBudget(startedAt, s.TimeoutMs);
            var evaluator = ExpressionEvaluatorFactory.Default;
            var name = await evaluator.EvaluateAsync<string>(s.TaskName, context).WaitAsync(TimeSpan.FromMilliseconds(Remaining()), cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(s.Values)) throw new ArgumentException("任务名及电压表达式不能为空");
            var values = AoOutputPlan.ConvertValues(await evaluator.EvaluateAsync<object>(s.Values, context).WaitAsync(TimeSpan.FromMilliseconds(Remaining()), cancellationToken).ConfigureAwait(false));
            guard.Check();
            var resource = NiDaqTaskRegistry.Get(context, name);
            guard.EnsureCurrent(name, resource);
            await resource.WriteAnalogAsync(values, Remaining(), cancellationToken).ConfigureAwait(false);
            guard.Check();
            guard.EnsureCurrent(name, resource);
            return new() { StepResult = new() { Status = TestStatus.Completed, Value = $"请求 [{string.Join(", ", values.Select(v => v.ToString("G17", CultureInfo.InvariantCulture)))}] V；SDK 已返回（非实测电压）" } };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested) { return new() { StepResult = new() { Status = TestStatus.Aborted } }; }
        catch (Exception ex) { return new() { StepResult = new() { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex, $"AO 单点写入失败：{ex.Message}") } }; }
    }
}
