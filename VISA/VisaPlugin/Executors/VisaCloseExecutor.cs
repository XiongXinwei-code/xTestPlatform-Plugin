using PluginCommon;
using Ivi.Visa;
using System.Diagnostics;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.Executors;

/// <summary>
/// VISA 关闭会话执行器，释放仪器连接资源
/// </summary>
public sealed class VisaCloseExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    /// <summary>执行 VISA 关闭会话操作</summary>
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new VisaClosePlugin().CreateSerializer();
            var setting = (VisaCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs));
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            epoch.ThrowIfExpired();
            if (string.IsNullOrWhiteSpace(connName)) throw new ArgumentException("VISA 连接名称不能为空");
            var key = VisaHelper.GetSessionKey(connName);

            var registration = VisaHelper.GetRegistrationLock(context.Resources, key);
            cancellationToken.ThrowIfCancellationRequested();
            var startedAt = Stopwatch.GetTimestamp();
            context.Resources.TryGet<object>(key, out var captured);
            epoch.ThrowIfExpired();
            var target = captured as VisaSessionResource;
            VisaHelper.MarkCloseRequested(context.Resources, key);
            if (captured != null && target == null) throw new InvalidOperationException("VISA 资源类型不匹配，不能确认其原始 I/O 已退出");
            target?.Dispose();
            await VisaHelper.WaitForRegistrationAsync(registration, VisaHelper.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken);
            try
            {
                epoch.ThrowIfExpired();
                if (target != null)
                {
                    epoch.ThrowIfExpired();
                    await target.CloseAsync(VisaHelper.RemainingBudget(startedAt, setting.TimeoutMs), cancellationToken);
                    epoch.ThrowIfExpired();
                    if (context.Resources.TryGet<VisaSessionResource>(key, out var current) && ReferenceEquals(current, target))
                    {
                        context.Resources.Remove(key, dispose: false);
                        context.Resources.Remove(VisaHelper.GetTerminatorKey(connName));
                    }
                }
            }
            finally { registration.Release(); }

            context.Log($"VISA 原代次关闭请求已完成: {connName}；不处理后来注册的新代次或其终止符");
            epoch.ThrowIfExpired();
            VisaResultPublisher.EnsureCallerActive(context, cancellationToken);
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = target == null ? $"原连接不存在: {connName}" : $"原连接已关闭: {connName}" }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
                    Error = ErrorInfo.FromException(ex, $"VISA 关闭失败: {ex.Message}")
                }
            };
        }
    }
}
