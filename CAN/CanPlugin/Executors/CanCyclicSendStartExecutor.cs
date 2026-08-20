using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanCyclicSendStartExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new CanCyclicSendStartPlugin().CreateSerializer();
        var setting = (CanCyclicSendStartSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            // 获取 CAN 适配器
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var adapterKey = CanHelper.GetAdapterKey(connName);
            if (!context.Resources.TryGet<ICanAdapter>(adapterKey, out var adapter))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"CAN 连接未找到: {connName}" }
                    }
                };
            }

            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            var taskKey = GetTaskKey(taskName);

            // 如果已有同名任务，先停止
            if (context.Resources.TryGet<CancellationTokenSource>(taskKey, out var existingCts))
            {
                await existingCts.CancelAsync();
                context.Resources.Remove(taskKey);
            }

            var cts = new CancellationTokenSource();
            context.Resources.Set(taskKey, cts);

            var enabledMessages = setting.Messages.Where(m => m.Enabled).ToList();
            if (enabledMessages.Count == 0)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Passed,
                        Value = "没有启用的报文，跳过周期发送"
                    }
                };
            }

            // 为每个报文启动独立的发送任务
            foreach (var msg in enabledMessages)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(msg.CycleTimeMs));
                        while (await timer.WaitForNextTickAsync(cts.Token))
                        {
                            // 每次发送前解析表达式，获取最新值
                            var canIdStr = await Evaluator.EvalStringAsync(msg.CanId, context);
                            var dataStr = await Evaluator.EvalStringAsync(msg.Data, context);

                            uint canId = CanHelper.ParseCanId(canIdStr);
                            byte[] data = CanHelper.ParseHexData(dataStr);

                            var message = new CanMessage
                            {
                                Id = canId,
                                FrameType = msg.FrameType,
                                Data = data,
                                IsFd = false
                            };

                            adapter.Write(message);

                            if (setting.EnableLog)
                            {
                                context.LogAction?.Invoke(
                                    $"[CyclicSend] 0x{canId:X} -> {BitConverter.ToString(data).Replace("-", " ")} ({msg.CycleTimeMs}ms)");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常停止
                    }
                    catch (Exception ex)
                    {
                        context.LogAction?.Invoke($"[CyclicSend] 报文 {msg.CanId} 发送异常: {ex.Message}");
                    }
                }, cts.Token);
            }

            context.LogAction?.Invoke($"CAN 周期发送已启动: TaskName={taskName}, 报文数={enabledMessages.Count}");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"已启动 {enabledMessages.Count} 个周期发送任务: {taskName}"
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
                    Error = ErrorInfo.FromException(ex, $"CAN 周期发送启动失败: {ex.Message}")
                }
            };
        }
    }

    internal static string GetTaskKey(string taskName) => $"CAN_CyclicSend_{taskName}";

    }
