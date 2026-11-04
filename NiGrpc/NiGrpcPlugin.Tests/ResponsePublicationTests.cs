using NiGrpc.Runtime;
using Xunit;

namespace NiGrpc.Tests;

public sealed class ResponsePublicationTests
{
    [Fact]
    public async Task Host_variable_write_can_still_occur_if_retirement_happens_after_check()
    {
        using var checkedOnce = new ManualResetEventSlim();
        using var releaseCheck = new ManualResetEventSlim();
        int retired = 0, checks = 0, writes = 0;
        var running = Task.Run(() => Assert.Throws<InvalidOperationException>(() => RemoteResultPublication.Publish(
            "WriteFrame", "xtest-can", () =>
            {
                if (Volatile.Read(ref retired) != 0) throw new InvalidOperationException("scope retired");
                if (Interlocked.Increment(ref checks) == 1) { checkedOnce.Set(); releaseCheck.Wait(); }
            }, () => Interlocked.Increment(ref writes))));
        Assert.True(checkedOnce.Wait(TimeSpan.FromSeconds(2)));
        Volatile.Write(ref retired, 1);
        releaseCheck.Set();
        await running.WaitAsync(TimeSpan.FromSeconds(2));
        // 这是对 SDK 缺少条件写入接口的限制复现，不是旧值写入正确性的验收。
        Assert.Equal(1, writes);
    }
    [Fact]
    public void Deadline_after_success_reports_ack_and_must_not_blindly_retry()
    {
        var error = Assert.Throws<InvalidOperationException>(() => RemoteResultPublication.Publish(
            "WriteAnalogF64", "xtest-owner", () => throw new TimeoutException("deadline"), () => { }));
        Assert.Contains("已收到服务端成功响应", error.Message);
        Assert.Contains("xtest-owner", error.Message);
        Assert.Contains("不能盲目重试", error.Message);
        Assert.IsType<TimeoutException>(error.InnerException);
    }

    [Fact]
    public void Output_failure_preserves_ack_and_failure_details()
    {
        var error = Assert.Throws<InvalidOperationException>(() => RemoteResultPublication.Publish(
            "WriteFrame", "xtest-can", () => { }, () => throw new InvalidOperationException("output failed")));
        Assert.Contains("已收到服务端成功响应", error.Message);
        Assert.Contains("WriteFrame", error.Message);
        Assert.Contains("output failed", error.InnerException!.Message);
    }
}
