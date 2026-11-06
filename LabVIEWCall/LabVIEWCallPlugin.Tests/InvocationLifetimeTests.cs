using LabVIEWCallPlugin.Execution;
using System.IO;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

public sealed class InvocationLifetimeTests
{
    [Fact]
    public async Task PreCancelledCallNeverEntersBridge()
    {
        var owner = new ViCallGate();
        int entered = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.InvokeAsync(
            "取消前", 5000, new CancellationToken(true), _ => ++entered));
        Assert.Equal(0, entered);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetachedCallKeepsOwnershipUntilActualExit(bool cancel)
    {
        var owner = new ViCallGate();
        using var caller = new CancellationTokenSource();
        using var finish = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int submitted = 0;
        var waiting = owner.InvokeAsync("第一次运行", cancel ? 5000 : 150, caller.Token, ct =>
        {
            Interlocked.Increment(ref submitted);
            entered.SetResult();
            finish.Wait(TimeSpan.FromSeconds(5));
            ct.ThrowIfCancellationRequested();
            return true;
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            if (cancel)
            {
                caller.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            }
            else await Assert.ThrowsAsync<TimeoutException>(() => waiting);
            var conflict = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                owner.InvokeAsync("第二次运行", 5000, default, _ => ++submitted));
            Assert.Contains("第一次运行", conflict.Message);
            Assert.Equal(1, submitted);
        }
        finally { finish.Set(); }
        await owner.WhenIdle.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(2, await owner.InvokeAsync("重跑", 5000, default, _ => ++submitted));
    }

    [Fact]
    public async Task NativeFailureDoesNotStrandGate()
    {
        var owner = new ViCallGate();
        await Assert.ThrowsAsync<IOException>(() => owner.InvokeAsync<int>(
            "报错", 5000, default, _ => throw new IOException("模拟原生异常")));
        Assert.Equal(42, await owner.InvokeAsync("重跑", 5000, default, _ => 42));
    }
}
