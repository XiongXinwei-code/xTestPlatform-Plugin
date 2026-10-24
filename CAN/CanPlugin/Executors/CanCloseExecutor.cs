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
                    try { await resource.CloseAsync(Remaining(), cancellationToken); }
                    finally
                    {
                        // 关闭后报告后台故障仍是 Error，但不让已释放的资源阻止重新打开。
                        if (resource.IsClosed && context.Resources.TryGet<ICanAdapter>(key, out var current) && ReferenceEquals(current, adapter)) context.Resources.Remove(key);
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

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed }
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
