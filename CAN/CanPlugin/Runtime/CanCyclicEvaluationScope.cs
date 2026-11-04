using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Runtime;

/// <summary>等待可取消，但真实求值始终计入周期任务退出；不允许旧结果重新进入发送路径。</summary>
internal sealed class CanCyclicEvaluationScope(int timeoutMs)
{
    private readonly object _gate = new();
    private readonly HashSet<Task> _pending = [];
    private bool _draining;
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    internal void Track(Task actual)
    {
        lock (_gate)
        {
            if (_draining) throw new InvalidOperationException("周期求值已进入收尾，不能再登记新求值");
            _pending.Add(actual);
        }
        Observe(actual);
    }

    private Task<T> StartTracked<T>(Func<Task<T>> operation)
    {
        Task<T> actual;
        lock (_gate)
        {
            if (_draining) throw new InvalidOperationException("周期求值已进入收尾，禁止启动旧表达式");
            actual = Task.Run(operation, CancellationToken.None);
            _pending.Add(actual);
        }
        Observe(actual); return actual;
    }

    private void Observe(Task actual)
    {
        _ = actual.ContinueWith(task =>
        {
            _ = task.Exception;
            lock (_gate) _pending.Remove(task);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    internal async Task<T> EvaluateAsync<T>(string expression, IExecutionContext context, CancellationToken ct, Action check)
    {
        check(); ct.ThrowIfCancellationRequested();
        string text = expression.Trim();
        // Constant editor values do not need a Roslyn/ThreadPool round-trip on every refresh.
        if (typeof(T) == typeof(bool) && text is "true" or "false") return (T)(object)(text == "true");
        if (typeof(T) == typeof(int) && int.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign,
            System.Globalization.CultureInfo.InvariantCulture, out int literal)) return (T)(object)literal;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeoutMs);
        var token = deadline.Token;
        var actual = StartTracked(async () =>
        {
            check(); token.ThrowIfCancellationRequested();
            var value = await Evaluator.EvaluateAsync<object>(expression, context).ConfigureAwait(false);
            check(); token.ThrowIfCancellationRequested();
            return value is T typed ? typed : throw new ArgumentException($"表达式必须返回 {typeof(T).Name}，实际为 {value?.GetType().Name ?? "null"}：{expression}");
        });
        try
        {
            var value = await actual.WaitAsync(token).ConfigureAwait(false);
            check(); token.ThrowIfCancellationRequested(); return value;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException($"CAN 周期表达式求值超过 {timeoutMs} ms，实际任务退出仍被跟踪：{expression}", ex);
        }
    }

    internal Task DrainAsync()
    {
        Task[] pending;
        lock (_gate) { _draining = true; pending = _pending.ToArray(); }
        return Task.WhenAll(pending.Select(task => task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)));
    }
}
