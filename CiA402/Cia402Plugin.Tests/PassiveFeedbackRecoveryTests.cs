using CAN.CANopen;
using CAN.Models;
using CiA402.Core;
using CiA402.Models;
using CiA402.Transport;
using xTestPlatform.Core.Models;
using Xunit;

namespace CiA402.Tests;

public sealed class PassiveFeedbackRecoveryTests
{
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 被动反馈取消后等待实际退出再继续诊断且不持续隔离(bool timeoutOnExit)
    {
        var cancelled = Signal(); var release = Signal();
        var probe = new ProbeAxisTransport { FeedbackIsPassiveReceive = true };
        probe.FeedbackHandler = async (_, _, _, ct) =>
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                await release.Task;
                if (timeoutOnExit) throw new TimeoutException("被动接收已结束，未得到帧");
                throw;
            }
            throw new InvalidOperationException("不应到达");
        };
        await using var transport = new CoordinatedAxisTransport(probe, 80);
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => transport.ReadFeedbackAsync(true, true, true, default));
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var diagnosis = transport.ReadObjectAsync(0x603F, 0, default);
            Assert.False(diagnosis.IsCompleted); // 必须排队，不得重入仍未退出的接收。
            Assert.Equal(1, probe.Calls);
            release.TrySetResult();
            Assert.Equal(new byte[] { 0, 0 }, await diagnosis);
            Assert.Equal(1, probe.MaximumActive);
            Assert.Contains("传输隔离=无", transport.DiagnosticContext);
            Assert.Empty(probe.Drive.Writes);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task 被动接收真正未退出时不提交写入且延后释放()
    {
        var release = new TaskCompletionSource<AxisFeedback>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new ProbeAxisTransport { FeedbackIsPassiveReceive = true, FeedbackHandler = (_, _, _, _) => release.Task };
        var transport = new CoordinatedAxisTransport(probe, 80);
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => transport.ReadFeedbackAsync(false, false, false, default));
            await Assert.ThrowsAsync<TimeoutException>(() => transport.WriteObjectAsync(0x6040, 0, [0, 0], default));
            Assert.Equal(1, probe.Calls);
            Assert.Empty(probe.Drive.Writes);
            transport.Dispose();
            Assert.False(probe.Disposed.Task.IsCompleted);
        }
        finally { release.TrySetResult(new(0x27, null, null, null)); await transport.DisposeAsync(); }
        Assert.Equal(1, probe.DisposeCount);
        Assert.False(probe.DisposedDuringCall);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 非被动对象调用超时即使后来结束仍保留隔离(bool write)
    {
        var release = Signal();
        var probe = new ProbeAxisTransport { FeedbackIsPassiveReceive = true };
        probe.ReadHandler = async (_, _) => { await release.Task; return [0, 0]; };
        probe.WriteHandler = async (_, _, _) => await release.Task;
        await using var transport = new CoordinatedAxisTransport(probe, 80);
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => write
                ? transport.WriteObjectAsync(0x6040, 0, [0, 0], default)
                : transport.ReadObjectAsync(0x603F, 0, default));
            release.TrySetResult();
            await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ReadObjectAsync(0x6041, 0, default));
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task 未声明被动能力的后端反馈超时仍保守隔离()
    {
        var release = new TaskCompletionSource<AxisFeedback>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new ProbeAxisTransport { FeedbackHandler = (_, _, _, _) => release.Task };
        await using var transport = new CoordinatedAxisTransport(probe, 80);
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => transport.ReadFeedbackAsync(false, false, false, default));
            release.TrySetResult(new(0x27, null, null, null));
            await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ReadObjectAsync(0x603F, 0, default));
        }
        finally { release.TrySetResult(new(0x27, null, null, null)); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CANopen仅在反馈配置为TPDO时声明被动能力(bool pdo)
    {
        using var adapter = new FakeCanAdapter();
        var config = new BindAxisSetting { FeedbackPdoNumber = pdo ? 1 : 0 };
        if (pdo) adapter.Pdo(1, true, 0x181, config.FeedbackPdoLayout);
        using var backend = new CanopenAxisTransport(CanopenConnectionAccess.For(adapter).ReserveNode(1, 1000), config);
        await backend.InitializeAsync(default);
        Assert.Equal(pdo, ((ICia402AxisTransport)backend).FeedbackIsPassiveReceive);
    }

    [Fact]
    public async Task CANopen缺TPDO后SDO诊断及下一次TPDO可继续且不关闭CAN()
    {
        using var adapter = new FakeCanAdapter();
        adapter.Object(0x603F, 0, 0, 2);
        var config = new BindAxisSetting { CommunicationTimeoutMs = 100, FeedbackPdoNumber = 1, FeedbackPdoLayout = "0x6041:0:16" };
        adapter.Pdo(1, true, 0x181, config.FeedbackPdoLayout);
        var backend = new CanopenAxisTransport(CanopenConnectionAccess.For(adapter).ReserveNode(1, 100), config);
        await backend.InitializeAsync(default);
        await using var transport = new CoordinatedAxisTransport(backend, config.CommunicationTimeoutMs);
        await Assert.ThrowsAsync<TimeoutException>(() => transport.ReadFeedbackAsync(false, false, false, default));
        Assert.Equal(new byte[] { 0, 0 }, await transport.ReadObjectAsync(0x603F, 0, default));
        adapter.Incoming.Enqueue(new CanMessage { Id = 0x181, Data = [0x37, 0x17], TimestampNs = 1 });
        Assert.Equal((ushort)0x1737, (await transport.ReadFeedbackAsync(false, false, false, default)).StatusWord);
        Assert.Equal(0, adapter.CloseCount);
    }

    [Fact]
    public async Task 原生读取不响应取消时被动恢复不能绕过共享CAN隔离()
    {
        using var adapter = new FakeCanAdapter();
        var config = new BindAxisSetting { CommunicationTimeoutMs = 1000, FeedbackPdoNumber = 1, FeedbackPdoLayout = "0x6041:0:16" };
        adapter.Pdo(1, true, 0x181, config.FeedbackPdoLayout);
        var connection = CanopenConnectionAccess.For(adapter);
        var backend = new CanopenAxisTransport(connection.ReserveNode(1, 1000), config);
        await backend.InitializeAsync(default);
        var transport = new CoordinatedAxisTransport(backend, 150);
        using var releaseNative = new ManualResetEventSlim();
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var nativeCancelled = Signal();
        var exited = Signal();
        adapter.ReadOverride = (_, ct) =>
        {
            using var cancellation = ct.Register(() => nativeCancelled.TrySetResult());
            entered.TrySetResult(ct);
            try { releaseNative.Wait(); return null; } // 模拟已收到取消令牌但原生读取未退出。
            finally { exited.TrySetResult(); }
        };
        var sentBefore = adapter.Sent.Count;
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => transport.ReadFeedbackAsync(false, false, false, default));
            var nativeToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(nativeToken.CanBeCanceled);
            await nativeCancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(nativeToken.IsCancellationRequested);
            await transport.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(exited.Task.IsCompleted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => connection.SendAsync(0x80, [], 1000, default));
            Assert.Equal(sentBefore, adapter.Sent.Count);
            Assert.Equal(0, adapter.CloseCount);
        }
        finally
        {
            releaseNative.Set();
            if (entered.Task.IsCompleted) await exited.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await transport.DisposeAsync();
        }
    }

    [Fact]
    public async Task 反馈步骤超时保留无效结果但不阻断后续PV控制()
    {
        var config = new BindAxisSetting { CommunicationTimeoutMs = 100 };
        var probe = new ProbeAxisTransport { FeedbackIsPassiveReceive = true };
        probe.Drive.Mode = OperationMode.PV;
        var controller = new AxisController(probe, config);
        await controller.InitializeAsync(default);
        probe.Drive.Status = 0x1737;
        probe.FeedbackHandler = async (_, _, _, ct) =>
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) { await Task.Delay(20); throw; }
            throw new InvalidOperationException();
        };
        using var session = new AxisSession(controller, config, default, (_, _) => { });
        var read = new ReadFeedbackPlugin();
        var (context, state) = TestContext.Create(read, new ReadFeedbackSetting { PositionVariable = "Locals.Position" });
        state.Variables["Locals.Position"] = -999d;
        state.Resources[AxisSession.Key("Axis1")] = session;
        Assert.Equal(TestStatus.Error, (await read.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
        Assert.Equal(-999d, state.Variables["Locals.Position"]); // 不把旧值当作新反馈成功返回。
        var velocity = new RunVelocityPlugin();
        var (nextContext, nextState) = TestContext.Create(velocity, new RunVelocitySetting { TargetVelocity = "42" });
        nextState.Resources = state.Resources;
        try
        {
            var result = await velocity.CreateExecutor().ExecuteAsync(nextContext);
            Assert.True(result.StepResult.Status == TestStatus.Completed, result.StepResult.Error?.Detail);
            Assert.Equal(42, ObjectCodec.Decode(probe.Drive.Objects[(0x60FF, 0)], 4, true));
            Assert.Equal(1, probe.MaximumActive);
        }
        finally { try { await session.CloseAsync(); } catch (Exception) { } }
    }
}
