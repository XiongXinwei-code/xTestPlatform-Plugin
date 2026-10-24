using CAN.CANopen;
using CAN.Helpers;
using CiA402.Core;
using CiA402.Models;
using CiA402.Transport;
using xTestPlatform.Core.Models;
using Xunit;

namespace CiA402.Tests;

public sealed class EnabledBindingRecoveryTests
{
    [Theory]
    [InlineData(0x1737)]
    [InlineData(0x0007)]
    [InlineData(0x0008)]
    public async Task 绑定既有驱动状态不写控制字模式或目标(int status)
    {
        var probe = new ProbeAxisTransport();
        probe.Drive.Status = (ushort)status;
        probe.Drive.Control = 0x007F;
        probe.Drive.Mode = OperationMode.PV;
        probe.Drive.Objects[(0x60FF, 0)] = ObjectCodec.Encode(123, 4, true);
        var config = new BindAxisSetting();
        var controller = new AxisController(probe, config);
        await controller.InitializeAsync(default);
        using var session = new AxisSession(controller, config, default, (_, _) => { });
        await session.ExecuteAsync(async (axis, ct) => { Assert.Equal((ushort)status, await axis.ReadStatusAsync(ct)); }, false, 1, default);
        await session.CloseAsync();
        Assert.Empty(probe.Drive.Writes);
        Assert.Equal((ushort)status, probe.Drive.Status);
        Assert.Equal(123, ObjectCodec.Decode(probe.Drive.Objects[(0x60FF, 0)], 4, true));
        Assert.Equal(1, probe.DisposeCount);
    }

    [Fact]
    public async Task CANopen绑定使能轴并只读解绑不发送停止或SYNC()
    {
        using var adapter = EnabledAdapter();
        var plugin = new BindAxisPlugin();
        var (context, state) = TestContext.Create(plugin, new BindAxisSetting());
        state.Resources[CanHelper.GetAdapterKey("CAN1")] = adapter;
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.True(result.StepResult.Status == TestStatus.Completed, result.StepResult.Error?.Detail);
        var session = Assert.IsType<AxisSession>(state.Resources[AxisSession.Key("Axis1")]);
        await session.CloseAsync();
        Assert.All(adapter.Sent, message => Assert.Equal(0x40, message.Data[0]));
        Assert.DoesNotContain(adapter.Sent, message => message.Id is 0 or 0x80);
        Assert.Equal(0x1737, ObjectCodec.Decode(adapter.Objects[(0x6041, 0)], 2, false));
        Assert.Equal(0, adapter.CloseCount);
    }

    [Fact]
    public async Task 绑定状态日志异常不撤回已使能轴的只读会话()
    {
        using var adapter = EnabledAdapter();
        var plugin = new BindAxisPlugin();
        var (context, state) = TestContext.Create(plugin, new BindAxisSetting());
        state.Resources[CanHelper.GetAdapterKey("CAN1")] = adapter;
        var logged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.OnLog = args =>
        {
            if (!args.OfType<string>().Any(text => text.Contains("已绑定：", StringComparison.Ordinal))) return;
            logged.TrySetResult();
            throw new InvalidOperationException("模拟绑定状态日志失败");
        };
        Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
        var session = Assert.IsType<AxisSession>(state.Resources[AxisSession.Key("Axis1")]);
        try
        {
            await logged.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await session.ExecuteAsync(async (axis, ct) => Assert.Equal((ushort)0x1737, await axis.ReadStatusAsync(ct)), false, 1, default);
            Assert.Contains("已终止=False", session.DiagnosticContext);
        }
        finally { await session.CloseAsync(); }
        Assert.DoesNotContain(adapter.Sent, message => message.Data[0] != 0x40);
    }

    [Fact]
    public async Task 隔离致清理失败驱动仍使能时可重新绑定并显式恢复PV()
    {
        using var adapter = EnabledAdapter();
        var oldConfig = new BindAxisSetting { CommunicationTimeoutMs = 80 };
        var oldBackend = new CanopenAxisTransport(CanopenConnectionAccess.For(adapter).ReserveNode(1, 80), oldConfig);
        var release = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new ProbeAxisTransport
        {
            ReadHandler = (index, ct) => index == 0x603F ? release.Task : oldBackend.ReadObjectAsync(index, 0, ct),
            WriteHandler = (index, value, ct) => oldBackend.WriteObjectAsync(index, 0, value, ct),
            DisposeAction = oldBackend.Dispose
        };
        var controller = new AxisController(probe, oldConfig);
        await controller.InitializeAsync(default);
        using var oldSession = new AxisSession(controller, oldConfig, default, (_, _) => { });
        await oldSession.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        try { await Assert.ThrowsAsync<TimeoutException>(() => controller.Transport.ReadObjectAsync(0x603F, 0, default)); }
        finally { release.TrySetResult([0, 0]); }
        var unbind = new UnbindAxisPlugin();
        var (closeContext, closeState) = TestContext.Create(unbind, new UnbindAxisSetting());
        closeState.Resources[CanHelper.GetAdapterKey("CAN1")] = adapter;
        closeState.Resources[AxisSession.Key("Axis1")] = oldSession;
        Assert.Equal(TestStatus.Error, (await unbind.CreateExecutor().ExecuteAsync(closeContext)).StepResult.Status);
        await oldSession.Released.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.DoesNotContain(adapter.Sent, message => message.Data[0] != 0x40);
        Assert.Equal(0x1737, ObjectCodec.Decode(adapter.Objects[(0x6041, 0)], 2, false));
        var bind = new BindAxisPlugin();
        var (bindContext, bindState) = TestContext.Create(bind, new BindAxisSetting());
        bindState.Resources = closeState.Resources;
        var reboundResult = await bind.CreateExecutor().ExecuteAsync(bindContext);
        Assert.True(reboundResult.StepResult.Status == TestStatus.Completed, reboundResult.StepResult.Error?.Detail);
        var rebound = Assert.IsType<AxisSession>(bindState.Resources[AxisSession.Key("Axis1")]);
        try
        {
            Assert.DoesNotContain(adapter.Sent, message => message.Data[0] != 0x40);
            var velocity = new RunVelocityPlugin();
            var (runContext, runState) = TestContext.Create(velocity, new RunVelocitySetting { TargetVelocity = "42" });
            runState.Resources = bindState.Resources;
            var result = await velocity.CreateExecutor().ExecuteAsync(runContext);
            Assert.True(result.StepResult.Status == TestStatus.Completed, result.StepResult.Error?.Detail);
            Assert.Equal(42, ObjectCodec.Decode(adapter.Objects[(0x60FF, 0)], 4, true));
        }
        finally { await rebound.CloseAsync(); }
        Assert.Equal(0, adapter.CloseCount);
    }

    private static FakeCanAdapter EnabledAdapter()
    {
        var adapter = new FakeCanAdapter();
        adapter.Object(0x6040, 0, 0x000F, 2);
        adapter.Object(0x6041, 0, 0x1737, 2);
        adapter.Object(0x6061, 0, (byte)OperationMode.PV, 1);
        adapter.Object(0x60FF, 0, 123, 4);
        return adapter;
    }
}
