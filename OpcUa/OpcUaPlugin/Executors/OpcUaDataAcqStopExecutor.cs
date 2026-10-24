using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 数据采集停止执行器</summary>
public sealed class OpcUaDataAcqStopExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Action? _afterCloseMarked;
    public OpcUaDataAcqStopExecutor() { }
    internal OpcUaDataAcqStopExecutor(Action afterCloseMarked) => _afterCloseMarked = afterCloseMarked;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new OpcUaExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new OpcUaDataAcqStopPlugin().CreateSerializer();
            var setting = (OpcUaDataAcqStopSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            guard.Check();
            var taskKey = $"OpcUaDataAcq_{taskName}";
            var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs));
            // 固定本次关闭请求捕获的实例；Mark 只使此前未完成的 Start 失效，不重取后来 owner。
            context.Resources.TryGet<OpcUaDataAcqTask>(taskKey, out var acqTask);
            guard.Check();
            OpcUaHelper.MarkCloseRequested(context.Resources, taskKey);
            _afterCloseMarked?.Invoke();
            if (acqTask == null)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"采集任务 {taskName} 不存在或未启动" }
                    }
                };
            }

            acqTask.RequestStop(); // 不等登记门即可冻结这个代次的后续读取。
            var registration = OpcUaHelper.GetRegistrationLock(context.Resources, taskKey);
            if (!await registration.WaitAsync(OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken))
                throw new TimeoutException("采集停止等待同名任务登记门超过总期限");
            List<DataAcqRecord> records;
            try
            {
                guard.Check();
                if (!context.Resources.TryGet<OpcUaDataAcqTask>(taskKey, out var current) || !ReferenceEquals(current, acqTask))
                    throw new InvalidOperationException("采集任务代次已改变，未关闭新任务，请重新执行停止");
                records = await acqTask.StopAsync(OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken);
                guard.Check();
                if (!context.Resources.TryGet<OpcUaDataAcqTask>(taskKey, out var stoppedCurrent) || !ReferenceEquals(stoppedCurrent, acqTask))
                    throw new InvalidOperationException("旧采集任务已停止，但表中已是新代次，未移除后来任务");
                context.Resources.Remove(taskKey, dispose: false);
                context.Resources.Remove(taskKey + "_items");
                acqTask.Dispose();
            }
            finally { registration.Release(); }

            if (acqTask.CancellationError is { } callbackError)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(callbackError, "OPC UA 采集已实际退出并移除，但底层取消回调失败；请检查异常详情") } };

            context.Log($"OPC UA 数据采集已停止: {taskName} (丢弃未消费数据 {records.Count} 条)");
            guard.Check();

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = $"采集已停止: {taskName}" }
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
                    Error = ErrorInfo.FromException(ex, $"停止数据采集失败: {ex.Message}")
                }
            };
        }
    }
}
