using System.Diagnostics;
using NiDaq.Helpers;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace NiDaq.Executors;

public sealed class NiDaqAoConfigExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        DaqTaskResource? configuredOwner = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new DaqExecutionGuard(context, cancellationToken);
            var epoch = guard.Epoch;
            long startedAt = Stopwatch.GetTimestamp();
            var step = context.CurrentStep?.Step ?? throw new InvalidOperationException("AO 配置缺少当前步骤上下文");
            guard.Check();
            var s = (NiDaqAoConfigSetting)new NiDaqAoConfigPlugin().CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            AoOutputPlan.ValidateConfiguration(s);
            if (string.IsNullOrWhiteSpace(s.InitialValues)) throw new ArgumentException("AO 初值表达式不能为空");
            var evaluator = ExpressionEvaluatorFactory.Default;
            int Remaining() => DaqTaskFactory.RemainingBudget(startedAt, s.TimeoutMs);
            var name = await evaluator.EvaluateAsync<string>(s.TaskName, context).WaitAsync(TimeSpan.FromMilliseconds(Remaining()), cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("AO 任务名求值结果不能为空");
            var initial = AoOutputPlan.ConvertValues(await evaluator.EvaluateAsync<object>(s.InitialValues, context).WaitAsync(TimeSpan.FromMilliseconds(Remaining()), cancellationToken).ConfigureAwait(false));
            double[]? final = null;
            if (s.StopPolicy == AoStopPolicy.WriteFinalValues)
            {
                if (string.IsNullOrWhiteSpace(s.FinalValues)) throw new ArgumentException("指定终值策略必须填写终值表达式");
                final = AoOutputPlan.ConvertValues(await evaluator.EvaluateAsync<object>(s.FinalValues, context).WaitAsync(TimeSpan.FromMilliseconds(Remaining()), cancellationToken).ConfigureAwait(false));
            }
            var plan = AoOutputPlan.Create(s, initial, final);
            var resource = await DaqTaskFactory.CreateAndRegisterAsync(context, name, owner => DaqTaskFactory.ConfigureAnalogOutput(owner, plan), cancellationToken,
                physicalChannels: plan.Channels.Select(c => c.PhysicalChannel), timeoutMs: Remaining(), epoch: epoch).ConfigureAwait(false);
            configuredOwner = resource;
            guard.EnsureCurrent(name, resource);
            epoch.ThrowIfExpired();
            return new() { StepResult = new() { Status = TestStatus.Completed, Value = $"AO 任务 '{name}' 已配置 {plan.Channels.Count} 通道（未写入电压），停止策略={plan.StopPolicy}" } };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested) { configuredOwner?.Dispose(); return new() { StepResult = new() { Status = TestStatus.Aborted } }; }
        catch (Exception ex) { configuredOwner?.Dispose(); return new() { StepResult = new() { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex, $"AO 配置失败：{ex.Message}") } }; }
    }
}
