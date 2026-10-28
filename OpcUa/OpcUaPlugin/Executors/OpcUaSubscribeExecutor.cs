using Opc.Ua;
using Opc.Ua.Client;
using System.Runtime.ExceptionServices;
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
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new OpcUaExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new OpcUaSubscribePlugin().CreateSerializer();
            var setting = (OpcUaSubscribeSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (!Enum.IsDefined(setting.CompareMode)) throw new ArgumentOutOfRangeException(nameof(setting.CompareMode), "未知 CompareMode，不能猜测比较条件");

            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var nodeIdStr = setting.NodeId;
            var expectedValue = await Evaluator.EvalStringAsync(setting.ExpectedValue, context);
            guard.Check();
            var key = OpcUaHelper.GetSessionKey(connName);

            if (!guard.TryGetResource<OpcUaConnectionResource>(key, out var connection))
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

            guard.EnsureCurrent(key, connection);
            var value = await WaitForValueAsync(connection, OpcUaHelper.ParseNodeId(nodeIdStr), expectedValue, setting, cancellationToken);
            guard.EnsureCurrent(key, connection);
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable)) context.SetVariable(setting.ResultVariable, value.Value);
            var text = value.Value?.ToString() ?? "";
            context.Log($"OPC UA 订阅: {nodeIdStr} = {text}，Good 条件满足，SourceTimestamp={value.SourceTimestamp:O}");
            guard.EnsureCurrent(key, connection);
            return ExecutionResults.Completed(text);
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
                    Error = ErrorInfo.FromException(ex, $"OPC UA 订阅失败: {ex.Message}")
                }
            };
        }
    }

    private static async Task<DataValue> WaitForValueAsync(OpcUaConnectionResource connection, NodeId nodeId,
        string expected, OpcUaSubscribeSetting setting, CancellationToken caller)
    {
        if (setting.TimeoutMs <= 0 || setting.SamplingIntervalMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(setting), "订阅期限与采样间隔必须大于 0");
        // 整个订阅（含真实 Create/Delete/取消回调）归连接的请求 owner，外层等候有界，后台收尾不脱离连接。
        return await connection.RunAsync(async token =>
        {
        Session? session = null;
        Subscription? subscription = null;
        Task? creation = null;
        Exception? failure = null;
        DataValue? matched = null;
        string lastQuality = "尚未读取";
        try
        {
            session = connection.Session;
            connection.ThrowIfSubmissionStopped();
            var current = await session.ReadValueAsync(nodeId, token).ConfigureAwait(false);
            connection.ThrowIfSubmissionStopped();
            lastQuality = current.StatusCode.ToString();
            if (OpcUaHelper.CompareValue(current, expected, setting.CompareMode)) matched = current;
            else
            {
                var done = new TaskCompletionSource<DataValue>(TaskCreationOptions.RunContinuationsAsynchronously);
                subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = setting.SamplingIntervalMs };
                var item = new MonitoredItem(subscription.DefaultItem)
                { StartNodeId = nodeId, AttributeId = Attributes.Value, SamplingInterval = setting.SamplingIntervalMs };
                item.Notification += (_, args) =>
                {
                    if (args.NotificationValue is not MonitoredItemNotification notification) return;
                    Volatile.Write(ref lastQuality, notification.Value.StatusCode.ToString());
                    if (OpcUaHelper.CompareValue(notification.Value, expected, setting.CompareMode)) done.TrySetResult(notification.Value);
                };
                subscription.AddItem(item);
                session.AddSubscription(subscription);
                var active = subscription;
                connection.ThrowIfSubmissionStopped();
                creation = active.CreateAsync(token);
                await creation.ConfigureAwait(false);
                connection.ThrowIfSubmissionStopped();
                var recheck = await session.ReadValueAsync(nodeId, token).ConfigureAwait(false);
                connection.ThrowIfSubmissionStopped();
                Volatile.Write(ref lastQuality, recheck.StatusCode.ToString());
                if (OpcUaHelper.CompareValue(recheck, expected, setting.CompareMode)) done.TrySetResult(recheck);
                matched = await done.Task.WaitAsync(token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            if (subscription != null)
            {
                var active = subscription;
                var cleanup = OpcUaSubscriptionCleanup.Start(creation,
                    t => active.Created ? active.DeleteAsync(true, t) : Task.CompletedTask,
                    () =>
                    {
                        var errors = new List<Exception>();
                        try { session!.RemoveSubscription(active); } catch (Exception ex) { errors.Add(ex); }
                        try { active.Dispose(); } catch (Exception ex) { errors.Add(ex); }
                        if (errors.Count > 0) throw new AggregateException("订阅本地移除/释放失败", errors);
                    }, null, ex => connection.Isolate($"订阅创建/清理失败：{ex.GetType().Name}"), setting.TimeoutMs);
                try
                {
                    // 原请求必须继续拥有真正的删除任务；不能由前台超时提前解除 Session 归属。
                    await cleanup.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    connection.Isolate("订阅收尾未成功确认，不能复用连接");
                    failure = failure == null ? ex : new AggregateException("OPC UA 订阅执行与清理均未成功", failure, ex);
                }
            }
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        return matched ?? throw new InvalidOperationException("OPC UA 订阅没有可用结果");
        }, setting.TimeoutMs, $"OPC UA 订阅 {nodeId} 等待 Good 条件及实际收尾", caller).ConfigureAwait(false);
    }
}
