using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 订阅执行器，等待节点值满足条件</summary>
public sealed class OpcUaSubscribeExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new OpcUaSubscribePlugin().CreateSerializer();
        var setting = (OpcUaSubscribeSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var nodeIdStr = await Evaluator.EvalStringAsync(setting.NodeId, context);
            var expectedValue = await Evaluator.EvalStringAsync(setting.ExpectedValue, context);
            var key = OpcUaHelper.GetSessionKey(connName);

            if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not Session session)
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

            var nodeId = OpcUaHelper.ParseNodeId(nodeIdStr);

            // 先读取当前值检查是否已满足条件
            var currentValue = await session.ReadValueAsync(nodeId, cancellationToken);
            var currentStr = currentValue.Value?.ToString() ?? "";
            if (OpcUaHelper.CompareValue(currentStr, expectedValue, setting.CompareMode))
            {
                context.SetVariable(setting.ResultVariable, currentValue.Value);
                context.LogAction?.Invoke($"OPC UA 订阅: {nodeIdStr} 当前值 {currentStr} 已满足条件");
                return new ExecutionResult
                {
                    StepResult = new StepResult { Status = TestStatus.Passed, Value = currentStr }
                };
            }

            // 创建订阅
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var subscription = new Subscription(session.DefaultSubscription)
            {
                PublishingInterval = setting.SamplingIntervalMs
            };

            var monitoredItem = new MonitoredItem(subscription.DefaultItem)
            {
                StartNodeId = nodeId,
                AttributeId = Attributes.Value,
                SamplingInterval = setting.SamplingIntervalMs
            };

            monitoredItem.Notification += (item, e) =>
            {
                if (e.NotificationValue is MonitoredItemNotification notification)
                {
                    var val = notification.Value.Value?.ToString() ?? "";
                    if (OpcUaHelper.CompareValue(val, expectedValue, setting.CompareMode))
                    {
                        tcs.TrySetResult(val);
                    }
                }
            };

            subscription.AddItem(monitoredItem);
            session.AddSubscription(subscription);
            await subscription.CreateAsync(cancellationToken);

            try
            {
                // 等待条件满足或超时
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(setting.TimeoutMs);

                using var reg = timeoutCts.Token.Register(() => tcs.TrySetCanceled());
                var resultValue = await tcs.Task;

                context.SetVariable(setting.ResultVariable, resultValue);
                context.LogAction?.Invoke($"OPC UA 订阅: {nodeIdStr} = {resultValue}，条件满足");

                return new ExecutionResult
                {
                    StepResult = new StepResult { Status = TestStatus.Passed, Value = resultValue }
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // 超时
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"OPC UA 订阅超时: {nodeIdStr} 在 {setting.TimeoutMs}ms 内未满足条件 {setting.CompareMode} {expectedValue}" }
                    }
                };
            }
            finally
            {
                // 清理订阅
                await subscription.DeleteAsync(true, cancellationToken.IsCancellationRequested ? CancellationToken.None : cancellationToken);
                session.RemoveSubscription(subscription);
            }
        }
        catch (OperationCanceledException)
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
                    Error = new ErrorInfo { Message = $"OPC UA 订阅失败: {ex.Message}" }
                }
            };
        }
    }
}
