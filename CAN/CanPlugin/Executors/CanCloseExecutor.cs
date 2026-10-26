using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using System.Diagnostics;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanCloseExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {

        try
        {
            var guard = new CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new CanClosePlugin().CreateSerializer();
            var setting = (CanCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.TimeoutMs);
            var budget = Stopwatch.StartNew();
            int Remaining()
            {
                int remaining = setting.TimeoutMs - (int)Math.Min(int.MaxValue, budget.ElapsedMilliseconds);
                if (remaining <= 0) throw new TimeoutException($"CAN 关闭总期限 {setting.TimeoutMs} ms 已到");
                return remaining;
            }
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = CanHelper.GetAdapterKey(connName);
            string? closeDiagnostics = null;
            guard.Check();
            context.Resources.TryGet<ICanAdapter>(key, out var expected);
            guard.Check();
            // Freeze this exact old owner immediately, even while its Open owns the registry gate.
            // Timeout/cancellation of the gate waiter must not allow a late Open to become Ready.
            if (expected is CanConnectionResource closingOwner) _ = closingOwner.RequestClose();
            using var lifecycle = await CanRegistryGuard.EnterAsync(context.Resources, connName, cancellationToken, Remaining());
            guard.Check();

            if (expected != null && context.Resources.TryGet<ICanAdapter>(key, out var adapter) && ReferenceEquals(adapter, expected))
            {
                if (adapter is CanConnectionResource resource)
                {
                    Exception? history = null;
                    try { await resource.ClosePhysicalAsync(Remaining(), cancellationToken); }
                    catch (Exception ex) when (resource.IsClosed && !cancellationToken.IsCancellationRequested)
                    { history = ex; }
                    finally
                    {
                        // Only verified physical release allows removal. Historical
                        // communication/stop errors remain diagnostic, not an open handle.
                        if (resource.IsClosed)
                        {
                            if (context.Resources.TryGet<ICanAdapter>(key, out var current) && ReferenceEquals(current, adapter)) context.Resources.Remove(key);
                            // An old managed expression/log may still be pending.
                            // It owns no physical driver and cannot hold this name.
                            lifecycle.Dispose();
                        }
                    }
                    try { await resource.WaitForWorkersAsync(Math.Max(0, setting.TimeoutMs - (int)Math.Min(int.MaxValue, budget.ElapsedMilliseconds)), cancellationToken); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception ex) when (resource.IsClosed && !resource.HasPendingManagedWorkers && ex is not TimeoutException)
                    { history = history == null ? ex : new AggregateException("CAN 已实际关闭，保留历史通信与回调错误", history, ex); }
                    catch (Exception ex) when (history != null)
                    { throw new AggregateException("CAN 物理句柄已释放，但后台退出尚未确认；同时保留历史关闭诊断", history, ex); }
                    if (history != null)
                    {
                        closeDiagnostics = history.ToString();
                        guard.Log(LogLevel.Warn, $"CAN 通道已确认关闭，历史通信/停止错误仍保留: {connName}；关闭不代表设备已停稳", closeDiagnostics);
                    }
                }
                else await Task.Run(adapter.Close, CancellationToken.None).WaitAsync(TimeSpan.FromMilliseconds(Remaining()), cancellationToken);
                if (context.Resources.TryGet<ICanAdapter>(key, out var currentAfterClose) && ReferenceEquals(currentAfterClose, adapter)) context.Resources.Remove(key);
                guard.Log($"CAN 通道已关闭: {connName}");
            }
            else
            {
                guard.Log(LogLevel.Warn, $"CAN 通道未找到: {connName}");
            }

            guard.Check();
            _ = Remaining();
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = closeDiagnostics ?? string.Empty }
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
                    Error = ErrorInfo.FromException(ex, $"关闭 CAN 通道失败: {ex.Message}")
                }
            };
        }
    }
}
