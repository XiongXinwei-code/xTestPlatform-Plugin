using NiGrpc.Runtime;
using Xunit;

namespace NiGrpc.Tests;

public sealed class ExpressionDeadlineTests
{
    [Fact]
    public async Task Blocked_expression_times_out_without_waiting_for_completion()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await Assert.ThrowsAsync<TimeoutException>(() => ExpressionHelper.EvalBoundedAsync(() => pending.Task, 30, default));
        pending.SetResult("late");
    }

    [Fact]
    public async Task Caller_can_cancel_wait_for_expression()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();
        var wait = ExpressionHelper.EvalBoundedAsync(() => pending.Task, 1000, stop.Token);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        pending.SetResult("late");
    }
}
