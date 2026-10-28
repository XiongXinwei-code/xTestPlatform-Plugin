using Opc.Ua.Client;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 数据采集启动执行器</summary>
public sealed class OpcUaDataAcqStartExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        OpcUaDataAcqTask? createdTask = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new OpcUaExecutionGuard(context, cancellationToken);
            var epoch = guard.Epoch;
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new OpcUaDataAcqStartPlugin().CreateSerializer();
            var setting = (OpcUaDataAcqStartSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs));
            void CheckStart()
            {
                guard.Check();
                _ = OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs);
            }
            CheckStart();
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context)
                .WaitAsync(TimeSpan.FromMilliseconds(OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs)), cancellationToken);
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context)
                .WaitAsync(TimeSpan.FromMilliseconds(OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs)), cancellationToken);
            CheckStart();
            var sessionKey = OpcUaHelper.GetSessionKey(connName);
            var taskKey = $"OpcUaDataAcq_{taskName}";
            long generation = OpcUaHelper.GetCloseGeneration(context.Resources, taskKey);

            // 获取 OPC UA 会话
            if (!guard.TryGetResource<OpcUaConnectionResource>(sessionKey, out var connection))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"OPC UA 连接 {connName} 不存在，请先执行 OpcUa_Connect" }
                    }
                };
            }

            if (setting.Items.Count == 0)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = "采集节点列表为空" }
                    }
                };
            }

            // 节点列表（NodeId 为字面值，不做表达式求值）
            var resolvedItems = new List<OpcUaDataAcqItem>();
            foreach (var item in setting.Items)
            {
                resolvedItems.Add(new OpcUaDataAcqItem { NodeId = item.NodeId, ColumnName = item.ColumnName });
            }

            if (string.IsNullOrWhiteSpace(taskName)) throw new InvalidOperationException("采集任务名称不能为空");
            var connectionRegistration = OpcUaHelper.GetRegistrationLock(context.Resources, sessionKey);
            if (!await connectionRegistration.WaitAsync(OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken))
                throw new TimeoutException("采集启动等待连接登记门超过总期限");
            try
            {
                CheckStart();
                // A reconnect may have completed while evaluating settings; do not attach to the old generation.
                if (!guard.TryGetResource<OpcUaConnectionResource>(sessionKey, out var current) || !ReferenceEquals(current, connection))
                    throw new InvalidOperationException("OPC UA 连接代次已改变，请重新执行采集启动");
                var registration = OpcUaHelper.GetRegistrationLock(context.Resources, taskKey);
                if (!await registration.WaitAsync(OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken))
                    throw new TimeoutException("采集启动等待同名任务登记门超过总期限");
                try
                {
                    CheckStart();
                    OpcUaHelper.ThrowIfOpenSuperseded(context.Resources, taskKey, generation);
                    if (guard.TryGetResource<OpcUaDataAcqTask>(taskKey, out var previous))
                    {
                        CheckStart();
                        OpcUaHelper.ThrowIfOpenSuperseded(context.Resources, taskKey, generation);
                        await previous.StopAsync(OpcUaHelper.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken);
                        CheckStart();
                        if (context.Resources.TryGet<OpcUaDataAcqTask>(taskKey, out var previousCurrent) && ReferenceEquals(previousCurrent, previous))
                            context.Resources.Remove(taskKey, dispose: false);
                        previous.Dispose();
                    }
                    CheckStart();
                    OpcUaHelper.ThrowIfOpenSuperseded(context.Resources, taskKey, generation);
                    var acqTask = connection.StartAcquisition(taskName, resolvedItems, setting.SamplingIntervalMs, setting.MaxDurationMs, setting.BufferSize);
                    createdTask = acqTask;
                    try
                    {
                        CheckStart();
                        OpcUaHelper.ThrowIfOpenSuperseded(context.Resources, taskKey, generation);
                        if (!context.Resources.TryAdd(taskKey, acqTask))
                            throw new InvalidOperationException($"OPC UA 采集任务 {taskName} 已存在，未覆盖旧任务");
                        CheckStart();
                        OpcUaHelper.ThrowIfOpenSuperseded(context.Resources, taskKey, generation);
                        context.Resources.Set(taskKey + "_items", resolvedItems);
                        CheckStart();
                    }
                    catch
                    {
                        acqTask.Dispose(); // 先冻结；connection 持有此 owner 至 CloseCompletion，不能把取消当真实退出。
                        if (context.Resources.TryGet<OpcUaDataAcqTask>(taskKey, out var registered) && ReferenceEquals(registered, acqTask))
                        {
                            context.Resources.Remove(taskKey, dispose: false);
                            if (context.Resources.TryGet<object>(taskKey + "_items", out var metadata) && ReferenceEquals(metadata, resolvedItems))
                                context.Resources.Remove(taskKey + "_items", dispose: false);
                        }
                        throw;
                    }
                }
                finally { registration.Release(); }
            }
            finally { connectionRegistration.Release(); }

            context.Log($"OPC UA 数据采集已启动: {taskName} ({resolvedItems.Count} 节点, {setting.SamplingIntervalMs}ms 间隔)");
            CheckStart();
            guard.EnsureCurrent(sessionKey, connection);
            if (!guard.TryGetResource<OpcUaDataAcqTask>(taskKey, out var published) || !ReferenceEquals(published, createdTask) || createdTask!.IsStopRequested)
                throw new InvalidOperationException("采集启动结果的资源代次已改变或已停止，不能报告成功");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = $"采集已启动: {taskName}" }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        {
            createdTask?.Dispose();
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            createdTask?.Dispose();
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"启动数据采集失败: {ex.Message}")
                }
            };
        }
    }
}
