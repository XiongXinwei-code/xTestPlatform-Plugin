using CAN.CANopen;
using CAN.Helpers;
using CiA402.Core;
using CiA402.Models;
using CiA402.Transport;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CiA402.Tests;

public sealed class BindingLifetimeTests
{
    [Fact]
    public async Task 绑定返回后取消绑定调用令牌不终止跨步骤反馈诊断并仍可回收()
    {
        using var adapter = new FakeCanAdapter();
        adapter.Object(0x6040, 0, 0, 2);
        adapter.Object(0x6041, 0, 0x40, 2);
        adapter.Object(0x603F, 0, 0, 2);
        using var bindingCaller = new CancellationTokenSource();
        var bindingPlugin = new BindAxisPlugin();
        var (bindingContext, bindingState) = TestContext.Create(bindingPlugin, new BindAxisSetting());
        bindingState.Resources[CanHelper.GetAdapterKey("CAN1")] = adapter;
        var result = await bindingPlugin.CreateExecutor().ExecuteAsync(bindingContext, bindingCaller.Token);
        Assert.True(result.StepResult.Status == TestStatus.Completed, result.StepResult.Error?.Detail);
        var session = Assert.IsType<AxisSession>(bindingState.Resources[AxisSession.Key("Axis1")]);
        try
        {
            bindingCaller.Cancel(); // 模拟绑定调用已结束后其短期令牌被宿主取消。
            foreach (var diagnostics in new[] { false, true })
            {
                var plugin = diagnostics
                    ? (xTestPlatform.Core.Plugins.Contracts.IStepPlugin)new ReadDiagnosticsPlugin() : new ReadFeedbackPlugin();
                var (readContext, readState) = diagnostics
                    ? TestContext.Create(plugin, new ReadDiagnosticsSetting { ErrorCodeVariable = "Locals.ErrorCode" })
                    : TestContext.Create(plugin, new ReadFeedbackSetting());
                readState.Resources = bindingState.Resources;
                readState.Variables["Locals.ErrorCode"] = -1d;
                var read = await plugin.CreateExecutor().ExecuteAsync(readContext);
                Assert.True(read.StepResult.Status == TestStatus.Completed, read.StepResult.Error?.Message);
                if (diagnostics) Assert.Equal(0d, readState.Variables["Locals.ErrorCode"]);
            }
            Assert.False(session.Released.IsCompleted);
            Assert.Contains("已终止=False", session.DiagnosticContext);
            Assert.Contains((AxisSession.Key("Axis1"), ResourceLifetime.Execution), bindingState.Registrations);
            // 模拟执行资源回收，不依赖已经失效的绑定令牌。仅标记控制所有权，不运行电机。
            await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
            Assert.True(bindingContext.Resources.Remove(AxisSession.Key("Axis1")));
            await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(0, adapter.CloseCount);
            Assert.True(adapter.IsConnected);
            Assert.Equal(new byte[] { 2, 0 }, adapter.Sent.Where(m => m.Data[0] == 0x2B && m.Data[1] == 0x40 && m.Data[2] == 0x60).Select(m => m.Data[4]).ToArray());
            using var next = CanopenConnectionAccess.For(adapter).ReserveNode(1, 1000);
        }
        finally { await session.CloseAsync(); }
    }

    [Fact]
    public async Task 绑定初始化中取消仍中止且不留下已注册会话()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new ProbeAxisTransport
        {
            ReadHandler = async (_, ct) => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return [0, 0]; }
        };
        var plugin = new BindAxisPlugin();
        var (context, state) = TestContext.Create(plugin, new BindAxisSetting { Transport = TransportKind.EtherCAT, CommunicationTimeoutMs = 10000, TimeoutMs = 30000 });
        state.Resources[ICia402EtherCatFactory.ResourceKey] = new Factory(probe);
        using var caller = new CancellationTokenSource();
        var execution = plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));
        caller.Cancel();
        Assert.Equal(TestStatus.Aborted, (await execution.WaitAsync(TimeSpan.FromSeconds(3))).StepResult.Status);
        Assert.False(state.Resources.ContainsKey(AxisSession.Key("Axis1")));
        await probe.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, probe.DisposeCount);
    }

    [Fact]
    public async Task 绑定完成后后续控制步骤自身取消仍终止并清理轴()
    {
        var probe = new ProbeAxisTransport();
        var bind = new BindAxisPlugin();
        var (context, state) = TestContext.Create(bind, new BindAxisSetting { Transport = TransportKind.EtherCAT, CommunicationTimeoutMs = 10000 });
        state.Resources[ICia402EtherCatFactory.ResourceKey] = new Factory(probe);
        Assert.Equal(TestStatus.Completed, (await bind.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
        var session = Assert.IsType<AxisSession>(state.Resources[AxisSession.Key("Axis1")]);
        try
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            probe.ReadHandler = async (_, ct) => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return [0, 0]; };
            var enable = new EnablePlugin();
            var (controlContext, controlState) = TestContext.Create(enable, new EnableSetting { TimeoutMs = 30000 });
            controlState.Resources = state.Resources;
            using var caller = new CancellationTokenSource();
            var control = enable.CreateExecutor().ExecuteAsync(controlContext, caller.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await control.WaitAsync(TimeSpan.FromSeconds(3))).StepResult.Status);
            await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Contains("控制步骤取消", session.DiagnosticContext);
            Assert.Equal(1, probe.DisposeCount);
        }
        finally { session.Dispose(); }
    }

    [Fact]
    public async Task 绑定注册时取消会撤回自己的新资源且不关闭CAN()
    {
        using var adapter = new FakeCanAdapter();
        adapter.Object(0x6040, 0, 0, 2);
        adapter.Object(0x6041, 0, 0x40, 2);
        using var caller = new CancellationTokenSource();
        var plugin = new BindAxisPlugin();
        var (context, state) = TestContext.Create(plugin, new BindAxisSetting());
        state.Resources[CanHelper.GetAdapterKey("CAN1")] = adapter;
        AxisSession? registering = null;
        state.OnResourceAdded = key =>
        {
            if (key != AxisSession.Key("Axis1")) return;
            registering = (AxisSession)state.Resources[key];
            caller.Cancel();
        };
        var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
        Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
        Assert.False(state.Resources.ContainsKey(AxisSession.Key("Axis1")));
        Assert.NotNull(registering);
        await registering.Released.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(adapter.IsConnected);
        Assert.Equal(0, adapter.CloseCount);
        using var next = CanopenConnectionAccess.For(adapter).ReserveNode(1, 1000);
    }

    private sealed class Factory(ICia402AxisTransport transport) : ICia402EtherCatFactory
    {
        public Task<ICia402AxisTransport> OpenAsync(AxisEndpoint endpoint, CancellationToken ct) => Task.FromResult(transport);
    }
}
