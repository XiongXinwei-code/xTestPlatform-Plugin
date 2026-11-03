using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace NiGrpc.Runtime;

internal static class ExpressionHelper
{
    internal static async Task<string> EvalStringAsync(this IExpressionEvaluator evaluator, string expression, IExecutionContext context)
        => await evaluator.EvaluateAsync<string>(expression, context) ?? "";

    internal static async Task<string> EvalBoundedAsync(Func<Task<string>> evaluate, int timeoutMs, CancellationToken ct, Action<Task>? track = null)
    {
        ct.ThrowIfCancellationRequested();
        var actual = Task.Run(evaluate);
        track?.Invoke(actual);
        _ = actual.ContinueWith(t => _ = t.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try { return await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false); }
        catch (TimeoutException ex) { throw new TimeoutException($"NI gRPC 表达式求值超过剩余期限 {timeoutMs} ms，未提交后续调用", ex); }
    }
}
