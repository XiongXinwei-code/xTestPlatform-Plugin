using Ivi.Visa;
using System.Diagnostics;
using PluginCommon;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.Executors;

/// <summary>
/// VISA 打开会话执行器，建立与仪器的 VISA 连接
/// </summary>
public sealed class VisaOpenExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Func<string, int, IDisposable> _openRaw;
    public VisaOpenExecutor() : this(VisaHelper.RawOpenSession) { }
    internal VisaOpenExecutor(Func<string, int, IDisposable> openRaw) => _openRaw = openRaw;

    /// <summary>执行 VISA 打开会话操作</summary>
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        VisaSessionResource? opening = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new VisaOpenPlugin().CreateSerializer();
            var setting = (VisaOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            epoch.ThrowIfExpired();
            var resource = await Evaluator.EvalStringAsync(setting.ResourceString, context);
            epoch.ThrowIfExpired();
            var key = VisaHelper.GetSessionKey(connName);

            if (string.IsNullOrWhiteSpace(connName)) throw new InvalidOperationException("VISA 连接名称不能为空");
            if (setting.OpenTimeoutMs == 0 || setting.OpenTimeoutMs < Timeout.Infinite) throw new ArgumentOutOfRangeException(nameof(setting.OpenTimeoutMs));
            if (setting.IoTimeoutMs == 0 || setting.IoTimeoutMs < Timeout.Infinite) throw new ArgumentOutOfRangeException(nameof(setting.IoTimeoutMs));
            if (setting.OpenTimeoutMs == Timeout.Infinite && setting.IoTimeoutMs == Timeout.Infinite)
                throw new ArgumentException("打开与 I/O 期限不能同时为无限等待，旧资源收尾需要有限期限");
            var startedAt = Stopwatch.GetTimestamp();
            int totalTimeoutMs = setting.OpenTimeoutMs;
            int RemainingPhase() => totalTimeoutMs == Timeout.Infinite
                ? setting.IoTimeoutMs > 0 ? setting.IoTimeoutMs : throw new InvalidOperationException("无限打开仍需配置有限的 I/O 期限用于旧会话收尾；不能同时为无限等待")
                : VisaHelper.RemainingBudget(startedAt, totalTimeoutMs);
            long generation = VisaHelper.GetCloseGeneration(context.Resources, key);
            var registration = VisaHelper.GetRegistrationLock(context.Resources, key);
            await VisaHelper.WaitForRegistrationAsync(registration,
                RemainingPhase(), cancellationToken);
            try
            {
                epoch.ThrowIfExpired();
                VisaHelper.ThrowIfOpenSuperseded(context.Resources, key, generation);
                if (context.Resources.TryGet<VisaSessionResource>(key, out var previous))
                {
                    // SDK回收和新运行登记可能发生在lookup中；旧Open不能先关闭新会话再报过期。
                    epoch.ThrowIfExpired();
                    VisaHelper.ThrowIfOpenSuperseded(context.Resources, key, generation);
                    await previous.CloseAsync(RemainingPhase(), cancellationToken);
                    epoch.ThrowIfExpired();
                    if (context.Resources.TryGet<VisaSessionResource>(key, out var previousCurrent) && ReferenceEquals(previousCurrent, previous))
                        context.Resources.Remove(key, dispose: false);
                }
                epoch.ThrowIfExpired();
                VisaHelper.ThrowIfOpenSuperseded(context.Resources, key, generation);
                var owner = new VisaSessionResource(setting.IoTimeoutMs, setting.Terminator,
                    ex => context.Log(LogLevel.Error, $"VISA {connName} 收尾未确认", ex.ToString()), resource);
                opening = owner;
                epoch.ThrowIfExpired();
                if (!context.Resources.TryAdd(key, owner))
                {
                    owner.Dispose();
                    throw new InvalidOperationException($"VISA 会话 {connName} 已存在，不能覆盖尚未确认关闭的资源");
                }
                try
                {
                    epoch.ThrowIfExpired();
                    VisaHelper.ThrowIfOpenSuperseded(context.Resources, key, generation);
                    await owner.InitializeAsync(() => { epoch.ThrowIfExpired(); return _openRaw(resource, setting.OpenTimeoutMs == Timeout.Infinite ? Timeout.Infinite : Math.Min(setting.OpenTimeoutMs, VisaHelper.RemainingBudget(startedAt, totalTimeoutMs))); },
                        session => VisaHelper.ConfigureSession(session, setting.IoTimeoutMs, setting.Terminator, () => { epoch.ThrowIfExpired(); owner.ThrowIfSubmissionStopped(); }),
                        VisaHelper.RemainingBudget(startedAt, totalTimeoutMs), cancellationToken);
                    epoch.ThrowIfExpired();
                    VisaHelper.ThrowIfOpenSuperseded(context.Resources, key, generation);
                    VisaResultPublisher.EnsureActive(context, key, owner, cancellationToken);
                    context.Resources.Set(VisaHelper.GetTerminatorKey(connName), owner.Terminator);
                }
                catch (Exception primary)
                {
                    if (primary is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    {
                        try { owner.Dispose(); }
                        catch (Exception cleanup) { throw new OperationCanceledException("VISA 打开已中断，后台收尾未确认，保留原 owner", new AggregateException(primary, cleanup), cancellationToken); }
                        throw;
                    }
                    try
                    {
                        owner.Dispose();
                        if (!owner.IsClosed) await owner.CloseAsync(RemainingPhase());
                        if (context.Resources.TryGet<VisaSessionResource>(key, out var current) && ReferenceEquals(current, owner))
                            context.Resources.Remove(key, dispose: false);
                    }
                    catch (Exception cleanup) { throw new AggregateException("VISA 打开失败且真实收尾未确认，资源保留，请重试关闭", primary, cleanup); }
                    throw;
                }
            }
            finally { registration.Release(); }

            context.Log($"VISA 会话已打开: {connName} ({resource})");
            epoch.ThrowIfExpired();
            VisaResultPublisher.EnsureActive(context, key, opening!, cancellationToken);

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = $"已连接: {resource}" }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            opening?.Dispose();
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            try { opening?.Dispose(); }
            catch (Exception cleanup) { ex = new AggregateException("VISA 建立失败且本次资源收尾未确认", ex, cleanup); }
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"VISA 打开失败: {ex.Message}")
                }
            };
        }
    }
}
