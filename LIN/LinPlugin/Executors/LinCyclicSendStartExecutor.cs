using LIN.Runtime;
using LIN.Adapters;
using LIN.Helpers;
using LIN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Executors;

public sealed class LinCyclicSendStartExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new LinExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new LinCyclicSendStartPlugin().CreateSerializer();
            var setting = (LinCyclicSendStartSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.ReplaceTimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.ReplaceTimeoutMs));
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int Remaining() => setting.ReplaceTimeoutMs > clock.ElapsedMilliseconds ? setting.ReplaceTimeoutMs - (int)clock.ElapsedMilliseconds : throw new TimeoutException($"LIN 周期启动/替换超过总期限 {setting.ReplaceTimeoutMs} ms");
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var adapterKey = LinHelper.GetAdapterKey(connName);
            if (!context.Resources.TryGet<ILinAdapter>(adapterKey, out var adapter))
            {
                guard.Check();
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"LIN 连接未找到: {connName}，请先执行 LIN_Open 步骤" }
                    }
                };
            }

            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            guard.Watch(adapterKey, adapter);
            var taskKey = GetTaskKey(taskName);

            using var registryLease = await LinRegistryGuard.Enter(context.Resources, cancellationToken, Remaining(), taskKey);
            guard.Check();
            var connection = LinConnectionResource.For(adapter);
            // 同名任务必须先确认真实退出
            if (context.Resources.TryGet<LinCyclicJob>(taskKey, out var existingCts))
            {
                await existingCts.StopAsync(Remaining(), cancellationToken);
                guard.Check();
                if (context.Resources.TryGet<LinCyclicJob>(taskKey, out var registered) && ReferenceEquals(registered, existingCts)) context.Resources.Remove(taskKey);
            }

            var enabledFrames = setting.Frames.Where(f => f.Enabled).ToList();
            if (enabledFrames.Count == 0)
            {
                guard.Check();
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Completed,
                        Value = "没有启用的帧，跳过周期发送"
                    }
                };
            }

            if (enabledFrames.Any(f => f.CycleTimeMs <= 0)) throw new ArgumentException("LIN 周期必须大于 0");
            if (enabledFrames.Any(f => f.ChecksumType != LinChecksumType.Enhanced)) throw new NotSupportedException("当前 LIN 无 LDF 周期发送仅支持 Enhanced 校验，请修改帧配置");
            var workers = new List<Func<CancellationToken, Task>>();

            // 为每个帧启动独立的发送任务
            foreach (var frameItem in enabledFrames)
            {
                workers.Add(async token =>
                {
                    try
                    {
                        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(frameItem.CycleTimeMs));
                        while (await timer.WaitForNextTickAsync(token))
                        {
                            var idStr = await Evaluator.EvalStringAsync(frameItem.FrameId, context);
                            var dataStr = await Evaluator.EvalStringAsync(frameItem.Data, context);

                            byte frameId = LinHelper.ParseFrameId(idStr);
                            byte[] data = LinHelper.ParseHexData(dataStr);

                            var frame = new LinFrame
                            {
                                FrameId = frameId,
                                Data = data,
                                ChecksumType = frameItem.ChecksumType
                            };

                            token.ThrowIfCancellationRequested();
                            guard.CheckLifetime();
                            connection.Write(frame, token, checkExecution: guard.CheckLifetime);

                            context.Log($"[LIN 周期发送] 0x{frameId:X2} → {BitConverter.ToString(data).Replace("-", " ")} ({frameItem.CycleTimeMs}ms)");
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
                    {
                        try { context.Log(LogLevel.Warn, $"LIN 周期任务失败，停止结果将返回错误：{ex.Message}", ex.ToString()); }
                        catch (Exception logError) { System.Diagnostics.Trace.TraceError($"LIN 日志回调失败：{logError}"); }
                        throw;
                    }
                });
            }

            guard.Check();
            var job = connection.StartJob(workers.ToArray());
            job.RegistrationKey = taskKey;
            try
            {
                guard.Check();
                if (!context.Resources.TryAdd(taskKey, job, ResourceLifetime.Execution)) throw new InvalidOperationException("LIN 周期任务名称已被其他步骤占用");
                guard.Check();
                guard.Log($"LIN 周期发送已启动: TaskName={taskName}, 帧数={enabledFrames.Count}");
            }
            catch { job.Dispose(); throw; } // Connection retains and joins this worker during cleanup.

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"已启动 {enabledFrames.Count} 个周期发送任务: {taskName}"
                }
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
                    Error = ErrorInfo.FromException(ex, $"LIN 周期发送启动失败: {ex.Message}")
                }
            };
        }
    }

    internal static string GetTaskKey(string taskName) => $"LIN_CyclicSend_{taskName}";
}
