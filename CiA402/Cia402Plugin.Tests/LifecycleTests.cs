using CiA402.Core;
using CiA402.Models;
using CiA402.Transport;
using CAN.CANopen;
using System.Diagnostics;
using CAN.Helpers;
using xTestPlatform.Core.Models;
using Xunit;

namespace CiA402.Tests;

public sealed class LifecycleTests
{
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task 同步资源回收不等待驱动释放()
    {
        using var release = new ManualResetEventSlim();
        var entered = Signal();
        var probe = new ProbeAxisTransport { DisposeAction = () => { entered.TrySetResult(); release.Wait(); } };
        var session = new AxisSession(new AxisController(probe, new BindAxisSetting()), new BindAxisSetting(), default, (_, _) => { });
        var disposing = Task.Run(session.Dispose);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await disposing.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.False(probe.Disposed.Task.IsCompleted);
        }
        finally
        {
            release.Set();
            await disposing.WaitAsync(TimeSpan.FromSeconds(3));
            await probe.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    [Fact]
    public async Task 底层Dispose阻塞时显式关闭有界且保留实际释放结果()
    {
        using var release = new ManualResetEventSlim();
        var entered = Signal();
        var probe = new ProbeAxisTransport { DisposeAction = () => { entered.TrySetResult(); release.Wait(); } };
        var config = new BindAxisSetting();
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        var clock = Stopwatch.StartNew();
        var closing = session.CloseAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<TimeoutException>(() => closing.WaitAsync(TimeSpan.FromSeconds(4)));
            Assert.True(clock.ElapsedMilliseconds < 4000);
            Assert.False(session.Released.IsCompleted);
            await Task.Run(session.Dispose).WaitAsync(TimeSpan.FromMilliseconds(500));
        }
        finally { release.Set(); }
        await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, probe.DisposeCount);
    }

    [Fact]
    public async Task 底层Dispose失败不冒充解除绑定成功()
    {
        var probe = new ProbeAxisTransport { DisposeAction = () => throw new InvalidOperationException("底层释放失败") };
        var config = new BindAxisSetting();
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => session.CloseAsync());
        Assert.Equal("底层释放失败", error.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.Released);
        Assert.Equal(1, probe.DisposeCount);
    }

    [Fact]
    public async Task 已取消的关闭请求不提交清理命令()
    {
        var probe = new ProbeAxisTransport();
        var config = new BindAxisSetting();
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.CloseAsync(cancelled.Token));
        await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, probe.Calls);
        Assert.Equal(1, probe.DisposeCount);
    }

    [Fact]
    public async Task 清理日志阻塞不阻止轴资源释放()
    {
        using var release = new ManualResetEventSlim();
        var logged = Signal();
        var probe = new ProbeAxisTransport { WriteHandler = (_, _, _) => Task.FromException(new InvalidOperationException("仿真通信失败")) };
        var config = new BindAxisSetting { CommunicationTimeoutMs = 100 };
        var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { logged.TrySetResult(); release.Wait(); });
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        var disposing = Task.Run(session.Dispose);
        try
        {
            await logged.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await probe.Disposed.Task.WaitAsync(TimeSpan.FromMilliseconds(500));
            await disposing.WaitAsync(TimeSpan.FromMilliseconds(500));
        }
        finally
        {
            release.Set();
            await disposing.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    [Fact]
    public async Task 控制失败及时返回原异常并在后台清理()
    {
        var release = Signal();
        var entered = Signal();
        var probe = new ProbeAxisTransport
        {
            WriteHandler = async (_, _, _) => { entered.TrySetResult(); await release.Task; }
        };
        var config = new BindAxisSetting { CommunicationTimeoutMs = 10000 };
        var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        var failed = session.ExecuteAsync((_, _) => Task.FromException(new InvalidOperationException("原始控制失败")), true, 1, default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => failed.WaitAsync(TimeSpan.FromMilliseconds(500)));
            Assert.Equal("原始控制失败", exception.Message);
        }
        finally
        {
            release.TrySetResult();
            try { await failed; } catch (InvalidOperationException) { }
            await session.CloseAsync();
            session.Dispose();
        }
    }

    [Fact]
    public async Task 轴清理期限不随单次通信超时增加()
    {
        var started = Signal();
        var probe = new ProbeAxisTransport
        {
            WriteHandler = async (_, _, ct) => { started.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); }
        };
        var config = new BindAxisSetting { CommunicationTimeoutMs = 60000 };
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        var clock = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(() => session.CloseAsync());
        Assert.True(clock.ElapsedMilliseconds < 4000, $"关闭耗时 {clock.ElapsedMilliseconds} ms");
        await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, probe.Calls); // 截止时间后不得再提交释放使能命令。
    }

    [Fact]
    public async Task 解除绑定取消后回收不追加等待或提交命令()
    {
        var entered = Signal();
        var release = Signal();
        var probe = new ProbeAxisTransport { WriteHandler = async (_, _, _) => { entered.TrySetResult(); await release.Task; } };
        var config = new BindAxisSetting { CommunicationTimeoutMs = 60000 };
        var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        using var cancel = new CancellationTokenSource();
        var closing = session.CloseAsync(cancel.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closing);
            await Task.Run(session.Dispose).WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.False(session.Released.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, probe.Calls);
        Assert.Equal(1, probe.DisposeCount);
    }

    [Fact]
    public async Task 并发停止和资源回收只释放一次()
    {
        var probe = new ProbeAxisTransport();
        var config = new BindAxisSetting();
        var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(session.Dispose)));
        await session.CloseAsync();
        await session.DisposeAsync();
        Assert.Equal(1, probe.DisposeCount);
        Assert.Equal(2, probe.Calls);
    }

    [Fact]
    public async Task 日志抛错不改变清理结果()
    {
        var probe = new ProbeAxisTransport();
        var config = new BindAxisSetting();
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => throw new InvalidOperationException("日志失败"));
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        await session.CloseAsync();
        Assert.Equal(1, probe.DisposeCount);
        Assert.Equal(0, probe.Drive.Control & 0xF);
    }

    [Fact]
    public async Task 解除绑定只释放节点不关闭共享CAN()
    {
        using var adapter = new FakeCanAdapter();
        var config = new BindAxisSetting { CommunicationTimeoutMs = 1000 };
        var connection = CanopenConnectionAccess.For(adapter);
        var node = connection.ReserveNode(1, 1000);
        using var session = new AxisSession(new AxisController(new CanopenAxisTransport(node, config), config), config, default, (_, _) => { });
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        await session.CloseAsync();
        await session.Released;
        Assert.True(adapter.IsConnected);
        Assert.Equal(0, adapter.CloseCount);
        Assert.Equal(new byte[] { 2, 0 }, adapter.Sent.Select(message => message.Data[4]).ToArray());
        using var rebound = connection.ReserveNode(1, 1000);
    }

    [Fact]
    public async Task 外部已关闭CAN时轴清理报告失败且不接管连接()
    {
        using var adapter = new FakeCanAdapter();
        var config = new BindAxisSetting();
        var node = CanopenConnectionAccess.For(adapter).ReserveNode(1, 1000);
        using var session = new AxisSession(new AxisController(new CanopenAxisTransport(node, config), config), config, default, (_, _) => { });
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        adapter.Close();
        await Assert.ThrowsAsync<AggregateException>(() => session.CloseAsync());
        Assert.Equal(1, adapter.CloseCount);
        Assert.False(adapter.IsConnected);
        Assert.Empty(adapter.Sent);
    }

    [Fact]
    public async Task CANopen无应答时轴清理有界且不关闭CAN()
    {
        using var adapter = new FakeCanAdapter { Respond = false };
        var config = new BindAxisSetting { CommunicationTimeoutMs = 60000 };
        var node = CanopenConnectionAccess.For(adapter).ReserveNode(1, 60000);
        using var session = new AxisSession(new AxisController(new CanopenAxisTransport(node, config), config), config, default, (_, _) => { });
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        var clock = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(() => session.CloseAsync());
        Assert.True(clock.ElapsedMilliseconds < 4000);
        await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(adapter.IsConnected);
        Assert.Equal(0, adapter.CloseCount);
        Assert.Single(adapter.Sent);
    }

    [Fact]
    public async Task 取消运行及时返回且只清理本轴()
    {
        var probe = new ProbeAxisTransport();
        var config = new BindAxisSetting();
        using var run = new CancellationTokenSource();
        using var session = new AxisSession(new AxisController(probe, config), config, run.Token, (_, _) => { });
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        await Task.Run(run.Cancel).WaitAsync(TimeSpan.FromMilliseconds(500));
        await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, probe.DisposeCount);
        Assert.Equal(0, probe.Drive.Control & 0xF);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync((_, _) => Task.CompletedTask, false, 1, default));
    }

    [Fact]
    public async Task 解除绑定失败仍移除终止会话并保留其他资源()
    {
        var probe = new ProbeAxisTransport { WriteHandler = (_, _, _) => Task.FromException(new InvalidOperationException("仿真写入失败")) };
        var config = new BindAxisSetting();
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        var plugin = new UnbindAxisPlugin();
        var (context, state) = TestContext.Create(plugin, new UnbindAxisSetting());
        var otherResource = new object();
        state.Resources[AxisSession.Key("Axis1")] = session;
        state.Resources["OtherResource"] = otherResource;
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.False(state.Resources.ContainsKey(AxisSession.Key("Axis1")));
        Assert.Same(otherResource, state.Resources["OtherResource"]);
        Assert.Equal(1, probe.DisposeCount);
    }

    [Fact]
    public async Task 绑定CANopen失败释放节点但保留CAN()
    {
        using var adapter = new FakeCanAdapter(); // 未提供初始化对象，返回 SDO Abort。
        var plugin = new BindAxisPlugin();
        var (context, state) = TestContext.Create(plugin, new BindAxisSetting());
        state.Resources[CanHelper.GetAdapterKey("CAN1")] = adapter;
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.False(state.Resources.ContainsKey(AxisSession.Key("Axis1")));
        Assert.True(adapter.IsConnected);
        Assert.Equal(0, adapter.CloseCount);
        using var next = CanopenConnectionAccess.For(adapter).ReserveNode(1, 1000);
    }

    [Fact]
    public async Task 绑定初始化失败不等待底层Dispose()
    {
        using var release = new ManualResetEventSlim();
        var entered = Signal();
        var probe = new ProbeAxisTransport
        {
            ReadHandler = (_, _) => Task.FromException<byte[]>(new InvalidOperationException("初始化读取失败")),
            DisposeAction = () => { entered.TrySetResult(); release.Wait(); }
        };
        var plugin = new BindAxisPlugin();
        var (context, state) = TestContext.Create(plugin, new BindAxisSetting { Transport = TransportKind.EtherCAT });
        state.Resources[ICia402EtherCatFactory.ResourceKey] = new Factory((_, _) => Task.FromResult<ICia402AxisTransport>(probe));
        var binding = plugin.CreateExecutor().ExecuteAsync(context);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));
            var result = await binding.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.False(state.Resources.ContainsKey(AxisSession.Key("Axis1")));
        }
        finally { release.Set(); }
        await probe.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, probe.DisposeCount);
    }

    [Fact]
    public async Task 同步阻塞工厂取消后不阻塞步骤且晚到实例释放一次()
    {
        using var release = new ManualResetEventSlim();
        var entered = Signal();
        var probe = new ProbeAxisTransport();
        var plugin = new BindAxisPlugin();
        var (context, state) = TestContext.Create(plugin, new BindAxisSetting { Transport = TransportKind.EtherCAT, TimeoutMs = 30000 });
        state.Resources[ICia402EtherCatFactory.ResourceKey] = new Factory((_, _) =>
        {
            entered.TrySetResult();
            release.Wait();
            return Task.FromResult<ICia402AxisTransport>(probe);
        });
        using var cancel = new CancellationTokenSource();
        var binding = plugin.CreateExecutor().ExecuteAsync(context, cancel.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));
            cancel.Cancel();
            var result = await binding.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            Assert.False(state.Resources.ContainsKey(AxisSession.Key("Axis1")));
        }
        finally { release.Set(); }
        await probe.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, probe.DisposeCount);
    }

    [Fact]
    public async Task 初始化失败后的异步释放异常会记录而不拖住步骤()
    {
        var logged = Signal();
        var probe = new ProbeAxisTransport
        {
            ReadHandler = (_, _) => Task.FromException<byte[]>(new InvalidOperationException("初始化读取失败")),
            DisposeAction = () => throw new InvalidOperationException("后端释放失败")
        };
        var plugin = new BindAxisPlugin();
        var (context, state) = TestContext.Create(plugin, new BindAxisSetting { Transport = TransportKind.EtherCAT });
        state.OnLog = arguments =>
        {
            if (arguments.OfType<string>().Any(s => s.Contains("初始化失败后释放轴通信实例失败"))) logged.TrySetResult();
        };
        state.Resources[ICia402EtherCatFactory.ResourceKey] = new Factory((_, _) => Task.FromResult<ICia402AxisTransport>(probe));
        var result = await plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        await logged.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, probe.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 同名轴解绑尚未结束时重新绑定排队且不被旧解绑移除(bool cancelWaiting)
    {
        var entered = Signal(); var release = Signal();
        var old = new ProbeAxisTransport { WriteHandler = async (_, _, _) => { entered.TrySetResult(); await release.Task; } };
        var config = new BindAxisSetting();
        using var session = new AxisSession(new AxisController(old, config), config, default, (_, _) => { });
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        var unbindPlugin = new UnbindAxisPlugin();
        var (closeContext, closeState) = TestContext.Create(unbindPlugin, new UnbindAxisSetting());
        closeState.Resources[AxisSession.Key("Axis1")] = session;
        var bindPlugin = new BindAxisPlugin();
        var (bindContext, bindState) = TestContext.Create(bindPlugin, new BindAxisSetting { Transport = TransportKind.EtherCAT });
        bindState.Resources = closeState.Resources;
        var next = new ProbeAxisTransport();
        bindState.Resources[ICia402EtherCatFactory.ResourceKey] = new Factory((_, _) => Task.FromResult<ICia402AxisTransport>(next));
        var closing = unbindPlugin.CreateExecutor().ExecuteAsync(closeContext);
        using var cancellation = new CancellationTokenSource();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var binding = bindPlugin.CreateExecutor().ExecuteAsync(bindContext, cancellation.Token);
            Assert.False(binding.IsCompleted);
            if (cancelWaiting)
            {
                cancellation.Cancel();
                Assert.Equal(TestStatus.Aborted, (await binding.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
                release.TrySetResult();
                Assert.Equal(TestStatus.Completed, (await closing).StepResult.Status);
                Assert.False(bindState.Resources.ContainsKey(AxisSession.Key("Axis1")));
                Assert.Equal(0, next.Calls);
                return;
            }
            release.TrySetResult();
            Assert.Equal(TestStatus.Completed, (await closing).StepResult.Status);
            Assert.Equal(TestStatus.Completed, (await binding).StepResult.Status);
            var rebound = Assert.IsType<AxisSession>(bindState.Resources[AxisSession.Key("Axis1")]);
            Assert.NotSame(session, rebound);
            await rebound.CloseAsync();
            Assert.Equal(1, old.DisposeCount);
            Assert.Equal(1, next.DisposeCount);
        }
        finally { release.TrySetResult(); }
    }

    private sealed class Factory(Func<AxisEndpoint, CancellationToken, Task<ICia402AxisTransport>> open) : ICia402EtherCatFactory
    {
        public Task<ICia402AxisTransport> OpenAsync(AxisEndpoint endpoint, CancellationToken ct) => open(endpoint, ct);
    }
}
