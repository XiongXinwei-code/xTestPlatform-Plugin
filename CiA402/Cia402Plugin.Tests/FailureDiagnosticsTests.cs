using System.Collections.Concurrent;
using CiA402.Core;
using CiA402.Models;
using CiA402.Transport;
using xTestPlatform.Core.Models;
using Xunit;

namespace CiA402.Tests;

public sealed class FailureDiagnosticsTests
{
    [Fact]
    public async Task 控制失败后反馈与诊断保留首次步骤和通信异常()
    {
        var config = new BindAxisSetting();
        var probe = new ProbeAxisTransport
        {
            ReadHandler = (_, _) => Task.FromException<byte[]>(new TimeoutException("模拟 SDO 原始超时")),
            WriteHandler = (_, _, _) => Task.FromException(new InvalidOperationException("后续清理也失败"))
        };
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        var enable = new EnablePlugin();
        var (context, state) = TestContext.Create(enable, new EnableSetting());
        state.Current!.StepName = "使能轴首故障";
        state.Current.StepAddress = "Main/7";
        state.Resources[AxisSession.Key("Axis1")] = session;
        var first = await enable.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, first.StepResult.Status);
        foreach (var diagnostics in new[] { false, true })
        {
            var (readContext, readState) = diagnostics
                ? TestContext.Create(new ReadDiagnosticsPlugin(), new ReadDiagnosticsSetting { ErrorCodeVariable = "Locals.ErrorCode" })
                : TestContext.Create(new ReadFeedbackPlugin(), new ReadFeedbackSetting());
            readState.Resources = state.Resources;
            readState.Variables["Locals.ErrorCode"] = 0d;
            var plugin = diagnostics ? (xTestPlatform.Core.Plugins.Contracts.IStepPlugin)new ReadDiagnosticsPlugin() : new ReadFeedbackPlugin();
            var result = await plugin.CreateExecutor().ExecuteAsync(readContext);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("首次终止", result.StepResult.Error!.Message);
            Assert.Contains("使能轴首故障", result.StepResult.Error.Message);
            Assert.Contains("模拟 SDO 原始超时", result.StepResult.Error.Message);
            Assert.DoesNotContain("后续清理也失败", result.StepResult.Error.Message);
            Assert.Contains("0x6041:00", result.StepResult.Error.Detail);
            Assert.Contains("Main/7", result.StepResult.Error.Detail);
        }
        await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task 只读失败不终止轴且返回详细错误而不重复打最终错误日志()
    {
        var config = new BindAxisSetting();
        var probe = new ProbeAxisTransport
        {
            ReadHandler = (_, _) => Task.FromException<byte[]>(new InvalidOperationException("模拟 SDO Abort 0x06020000"))
        };
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        var plugin = new ReadDiagnosticsPlugin();
        var (context, state) = TestContext.Create(plugin, new ReadDiagnosticsSetting { ErrorCodeVariable = "Locals.ErrorCode" });
        state.Current!.StepName = "读取故障码";
        state.Resources[AxisSession.Key("Axis1")] = session;
        state.Variables["Locals.ErrorCode"] = 0d;
        var logs = new ConcurrentQueue<string>();
        state.OnLog = args => logs.Enqueue(string.Join(" ", args));
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("Axis1", result.StepResult.Error!.Detail);
        Assert.Contains("0x603F:00", result.StepResult.Error.Detail);
        Assert.Contains("读取故障码", result.StepResult.Error.Detail);
        Assert.Contains("CommunicationTimeoutMs=1000", result.StepResult.Error.Detail);
        Assert.DoesNotContain(logs, log => log.Contains("CiA402 步骤执行错误"));
        probe.ReadHandler = null;
        Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
        await session.CloseAsync();
    }

    [Fact]
    public async Task 显式会话生命周期令牌取消记录真实来源()
    {
        var config = new BindAxisSetting();
        using var token = new CancellationTokenSource();
        using var session = new AxisSession(new AxisController(new ProbeAxisTransport(), config), config, token.Token, (_, _) => { });
        token.Cancel();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync((_, _) => Task.CompletedTask, false, 1, default));
        Assert.Contains("显式轴会话生命周期令牌取消", error.Message);
        await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task 显式关闭后读取显示关闭来源而不是假称通信失败()
    {
        var config = new BindAxisSetting();
        using var session = new AxisSession(new AxisController(new ProbeAxisTransport(), config), config, default, (_, _) => { });
        await session.CloseAsync();
        session.Dispose();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync((_, _) => Task.CompletedTask, false, 1, default));
        Assert.Contains("显式关闭", error.Message);
        Assert.DoesNotContain("资源回收", error.Message);
    }

    [Fact]
    public async Task 通信软超时说明对象与I_O阶段并保留原始隔离原因()
    {
        var release = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new ProbeAxisTransport { ReadHandler = (_, _) => release.Task };
        await using var transport = new CoordinatedAxisTransport(probe, 80);
        try
        {
            var error = await Assert.ThrowsAsync<TimeoutException>(() => transport.ReadObjectAsync(0x603F, 0, default));
            Assert.Contains("0x603F:00", error.Message);
            var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ReadObjectAsync(0x6041, 0, default));
            Assert.Contains("0x603F:00", blocked.Message);
            Assert.Contains("尚未退出", blocked.Message);
        }
        finally { release.TrySetResult([0, 0]); }
    }

    [Fact]
    public async Task 步骤总期限与调用方取消分别保留详细信息()
    {
        foreach (var abort in new[] { false, true })
        {
            var config = new BindAxisSetting { CommunicationTimeoutMs = 10000 };
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var probe = new ProbeAxisTransport { ReadHandler = async (_, ct) => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return [0, 0]; } };
            using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
            var plugin = new EnablePlugin();
            var settings = new EnableSetting { TimeoutMs = abort ? 10000 : 1000, PollIntervalMs = 1 };
            var (context, state) = TestContext.Create(plugin, settings);
            state.Current!.StepName = "使能等待阶段";
            state.Resources[AxisSession.Key("Axis1")] = session;
            var logs = new ConcurrentQueue<string>();
            state.OnLog = args => logs.Enqueue(string.Join(" ", args));
            using var caller = new CancellationTokenSource();
            // 表达式首次编译耗时也计入步骤期限；先预热，确保本用例检查的是进入控制后的取消。
            await SettingValidation.EvaluateAsync(settings, context, default);
            var execution = plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            if (abort) caller.Cancel();
            var result = await execution;
            Assert.Equal(abort ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            var detail = abort ? string.Join("\n", logs) : result.StepResult.Error!.Detail;
            Assert.Contains("使能等待阶段", detail);
            Assert.Contains($"调用方令牌取消={abort}", detail);
            Assert.Contains("步骤令牌取消=True", detail);
            Assert.Contains("TimeoutMs=", detail);
            Assert.Contains("控制步骤取消", session.DiagnosticContext);
            await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    [Fact]
    public async Task 一根轴终止不会终止另一根轴且Dispose不会覆盖第一次原因()
    {
        var config = new BindAxisSetting();
        using var first = new AxisSession(new AxisController(new ProbeAxisTransport(), config), config, default, (_, _) => { });
        using var second = new AxisSession(new AxisController(new ProbeAxisTransport(), config), config, default, (_, _) => { });
        var root = new InvalidOperationException("首个控制错误");
        Assert.Same(root, await Assert.ThrowsAsync<InvalidOperationException>(() => first.ExecuteAsync((_, _) => Task.FromException(root), true, 1, default, "主轴运动")));
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(first.Dispose)));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => first.ExecuteAsync((_, _) => Task.CompletedTask, false, 1, default));
        Assert.Contains("主轴运动", error.Message);
        Assert.Same(root, error.InnerException);
        await second.ExecuteAsync((_, _) => Task.CompletedTask, false, 1, default);
        await first.Released.WaitAsync(TimeSpan.FromSeconds(3));
        await second.CloseAsync();
    }

    [Fact]
    public async Task 清理日志回调抛错不会掩盖首故障及后续错误详情()
    {
        var config = new BindAxisSetting();
        using var session = new AxisSession(new AxisController(new ProbeAxisTransport(), config), config, default, (_, _) => throw new Exception("日志写入异常"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync((_, _) => Task.FromException(new InvalidOperationException("原始驱动错误")), true, 1, default, "测试控制"));
        await session.Released.WaitAsync(TimeSpan.FromSeconds(3));
        var plugin = new ReadFeedbackPlugin();
        var (context, state) = TestContext.Create(plugin, new ReadFeedbackSetting());
        state.OnLog = _ => throw new Exception("宿主最终日志接口异常");
        state.Resources[AxisSession.Key("Axis1")] = session;
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Contains("原始驱动错误", result.StepResult.Error!.Message);
        Assert.Contains("测试控制", result.StepResult.Error.Detail);
    }

    [Fact]
    public async Task TPDO外层超时仍能找到缺失ID及重复丢弃计数()
    {
        using var adapter = new FakeCanAdapter();
        var config = new BindAxisSetting { CommunicationTimeoutMs = 120, FeedbackMaxAgeMs = 1000, UseMultipleFeedbackPdos = true,
            FeedbackPdos = [new() { Number = 1, Layout = "0x6041:0:16" }, new() { Number = 2, Layout = "0x606C:0:32" }] };
        adapter.Pdo(1, true, 0x181, config.FeedbackPdos[0].Layout);
        adapter.Pdo(2, true, 0x281, config.FeedbackPdos[1].Layout);
        var backend = new CanopenAxisTransport(CAN.CANopen.CanopenConnectionAccess.For(adapter).ReserveNode(1, 120), config);
        await backend.InitializeAsync(default);
        adapter.Incoming.Enqueue(new CAN.Models.CanMessage { Id = 0x181, Data = [0x27, 0], TimestampNs = 10 });
        await backend.ReadFeedbackAsync(false, false, false, default);
        adapter.Incoming.Enqueue(new CAN.Models.CanMessage { Id = 0x181, Data = [0x27, 0], TimestampNs = 10 });
        await using var transport = new CoordinatedAxisTransport(backend, config.CommunicationTimeoutMs);
        var error = await Assert.ThrowsAsync<TimeoutException>(() => transport.ReadFeedbackAsync(false, true, false, default));
        var detail = Assert.IsType<string>(error.Data["CiA402.Transport"]);
        Assert.Contains("0x181", detail);
        Assert.Contains("0x281", detail);
        Assert.Contains("丢弃重复时间戳=1", detail);
        Assert.Contains("缺失/过期", detail);
        Assert.Equal(0, adapter.CloseCount);
    }

    [Fact]
    public async Task 排队取消明确标识阶段且不错误隔离正在工作的传输()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new ProbeAxisTransport { ReadHandler = (_, _) => { entered.TrySetResult(); return release.Task; } };
        await using var transport = new CoordinatedAxisTransport(probe, 10000);
        var active = transport.ReadObjectAsync(0x6041, 0, default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            using var cancel = new CancellationTokenSource(80);
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.ReadObjectAsync(0x603F, 0, cancel.Token));
            var detail = Assert.IsType<string>(error.Data["CiA402.Transport"]);
            Assert.Contains("等待 I/O 排队", detail);
            Assert.Contains("0x603F:00", detail);
            Assert.Contains("传输隔离=无", transport.DiagnosticContext);
        }
        finally { release.TrySetResult([0x40, 0]); }
        await active;
        await transport.ReadObjectAsync(0x6041, 0, default);
    }

    [Fact]
    public async Task 第三方只读异常Data不导致诊断替换原始异常()
    {
        var root = new ReadOnlyDataException();
        var probe = new ProbeAxisTransport { ReadHandler = (_, _) => Task.FromException<byte[]>(root) };
        await using var transport = new CoordinatedAxisTransport(probe, 1000);
        Assert.Same(root, await Assert.ThrowsAsync<ReadOnlyDataException>(() => transport.ReadObjectAsync(0x603F, 0, default)));
        Assert.Contains("0x603F:00", transport.DiagnosticContext);
    }

    private sealed class ReadOnlyDataException : Exception
    {
        public override System.Collections.IDictionary Data { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<object, object>(new Dictionary<object, object>());
    }

    [Fact]
    public async Task 会话关闭取消只读操作不能被误报为步骤总超时()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var config = new BindAxisSetting { CommunicationTimeoutMs = 10000 };
        var probe = new ProbeAxisTransport { ReadHandler = async (_, ct) => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return [0, 0]; } };
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        var plugin = new ReadDiagnosticsPlugin();
        var (context, state) = TestContext.Create(plugin, new ReadDiagnosticsSetting { ErrorCodeVariable = "Locals.ErrorCode", TimeoutMs = 10000 });
        state.Variables["Locals.ErrorCode"] = 0d;
        state.Resources[AxisSession.Key("Axis1")] = session;
        var reading = plugin.CreateExecutor().ExecuteAsync(context);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var closing = session.CloseAsync();
        var result = await reading;
        await closing;
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("退出", result.StepResult.Error!.Message);
        Assert.DoesNotContain("总超时", result.StepResult.Error.Message);
        Assert.Contains("显式关闭", result.StepResult.Error.Detail);
        Assert.Contains("步骤令牌取消=False", result.StepResult.Error.Detail);
    }

    [Fact]
    public async Task 解除绑定失败移除资源后仍保留失败时轴上下文()
    {
        var config = new BindAxisSetting { CommunicationTimeoutMs = 1234 };
        var probe = new ProbeAxisTransport { WriteHandler = (_, _, _) => Task.FromException(new InvalidOperationException("模拟驱动写入失败")) };
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { }, "轴=Axis1; 连接=已解析总线; 节点=3");
        await session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default);
        var plugin = new UnbindAxisPlugin();
        var (context, state) = TestContext.Create(plugin, new UnbindAxisSetting());
        state.Current!.StepName = "显式解除绑定";
        state.Resources[AxisSession.Key("Axis1")] = session;
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.False(state.Resources.ContainsKey(AxisSession.Key("Axis1")));
        Assert.Contains("已解析总线", result.StepResult.Error!.Detail);
        Assert.Contains("CommunicationTimeoutMs=1234", result.StepResult.Error.Detail);
        Assert.Contains("显式解除绑定", result.StepResult.Error.Detail);
        Assert.Contains("0x6040:00", result.StepResult.Error.Detail);
    }
}
