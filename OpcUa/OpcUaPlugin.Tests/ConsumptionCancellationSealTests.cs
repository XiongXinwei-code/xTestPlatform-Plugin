using OpcUa.Helpers;
using Xunit;

namespace OpcUa.Tests;

public sealed class ConsumptionCancellationSealTests
{
    [Fact]
    public async Task CancellationJoinCannotBeInvalidatedByLateAbandon()
    {
        var operation = new OpcUaConsumptionQueue.Consumption(30000, default, () => false);
        using var callbackExit = new ManualResetEventSlim();
        int calls = 0;
        var registration = operation.Token.Register(() =>
        { Interlocked.Increment(ref calls); callbackExit.Wait(); });
        try
        {
            // 精确重放 actual finally 先取完成任务，observer catch 随后 Abandon 的交错。
            // 不依赖线程池是否恰好在两行代码之间切换。
            var join = operation.SealCancellationAndGetCompletion();
            Assert.True(join.IsCompletedSuccessfully);
            operation.Abandon();
            Assert.False(operation.Token.IsCancellationRequested);
            Assert.Equal(0, Volatile.Read(ref calls));
            await join;
            Assert.False(await operation.Accepted);
        }
        finally
        {
            callbackExit.Set();
            try { await operation.SealCancellationAndGetCompletion(); }
            finally { registration.Dispose(); operation.Dispose(); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbandonBeforeJoinKeepsActualCallbackAndItsFailureOwned(bool callbackFails)
    {
        var operation = new OpcUaConsumptionQueue.Consumption(30000, default, () => false);
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callbackExit = new ManualResetEventSlim();
        var registration = operation.Token.Register(() =>
        {
            callbackEntered.TrySetResult(); callbackExit.Wait();
            if (callbackFails) throw new InvalidOperationException("consumer cancellation callback failed");
        });
        try
        {
            operation.Abandon();
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var join = operation.SealCancellationAndGetCompletion();
            Assert.False(join.IsCompleted);
            operation.Abandon();
            Assert.Same(join, operation.SealCancellationAndGetCompletion());
            callbackExit.Set();
            if (callbackFails)
            {
                var error = await Assert.ThrowsAsync<AggregateException>(() => join);
                Assert.Contains("consumer cancellation callback failed", error.ToString());
            }
            else await join;
        }
        finally
        {
            callbackExit.Set();
            try { await operation.SealCancellationAndGetCompletion(); }
            catch (AggregateException) when (callbackFails) { }
            finally { registration.Dispose(); operation.Dispose(); }
        }
    }
}
