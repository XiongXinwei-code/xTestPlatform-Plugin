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
        var step = context.CurrentStep!.Step;
        var serializer = new LinCyclicSendStartPlugin().CreateSerializer();
        var setting = (LinCyclicSendStartSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var adapterKey = LinHelper.GetAdapterKey(connName);
            if (!context.Resources.TryGet<ILinAdapter>(adapterKey, out var adapter))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error  = new ErrorInfo { Message = $"LIN 连接未找到: {connName}，请先执行 LIN_Open 步骤" }
                    }
                };
            }

            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            var taskKey  = GetTaskKey(taskName);

            // 如果已有同名任务，先停止
            if (context.Resources.TryGet<CancellationTokenSource>(taskKey, out var existingCts))
            {
                await existingCts.CancelAsync();
                context.Resources.Remove(taskKey);
            }

            var enabledFrames = setting.Frames.Where(f => f.Enabled).ToList();
            if (enabledFrames.Count == 0)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Passed,
                        Value  = "没有启用的帧，跳过周期发送"
                    }
                };
            }

            var cts = new CancellationTokenSource();
            context.Resources.Set(taskKey, cts);

            // 为每个帧启动独立的发送任务
            foreach (var frameItem in enabledFrames)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(frameItem.CycleTimeMs));
                        while (await timer.WaitForNextTickAsync(cts.Token))
                        {
                            var idStr   = await Evaluator.EvalStringAsync(frameItem.FrameId, context);
                            var dataStr = await Evaluator.EvalStringAsync(frameItem.Data, context);

                            byte frameId = LinHelper.ParseFrameId(idStr);
                            byte[] data  = LinHelper.ParseHexData(dataStr);

                            var frame = new LinFrame
                            {
                                FrameId      = frameId,
                                Data         = data,
                                ChecksumType = frameItem.ChecksumType
                            };

                            adapter.Write(frame);

                            if (setting.EnableLog)
                                context.LogAction?.Invoke($"[LIN 周期发送] 0x{frameId:X2} → {BitConverter.ToString(data).Replace("-", " ")} ({frameItem.CycleTimeMs}ms)");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常停止
                    }
                    catch (Exception ex)
                    {
                        context.LogAction?.Invoke($"[LIN 周期发送] 帧 {frameItem.FrameId} 发送异常: {ex.Message}");
                    }
                }, cts.Token);
            }

            context.LogAction?.Invoke($"LIN 周期发送已启动: TaskName={taskName}, 帧数={enabledFrames.Count}");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value  = $"已启动 {enabledFrames.Count} 个周期发送任务: {taskName}"
                }
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
                    Error  = ErrorInfo.FromException(ex, $"LIN 周期发送启动失败: {ex.Message}")
                }
            };
        }
    }

    internal static string GetTaskKey(string taskName) => $"LIN_CyclicSend_{taskName}";
}
