using CAN.CANopen;
using CAN.Models;
using CiA402.Core;
using CiA402.Models;
using CiA402.Transport;
using CiA402.UI.Validation;
using CiA402.UI.ViewModels;
using MessagePack;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace CiA402.Tests;

public sealed class ConcurrencyAndModeTests
{
    private static void Check(bool value, string message = "断言不成立") => Assert.True(value, message);
    private static Task<T> Throws<T>(Func<Task> action) where T : Exception => Assert.ThrowsAnyAsync<T>(action);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static async Task<(AxisController Axis, FakeAxisTransport Drive)> Ready(OperationMode mode = OperationMode.PP, bool monitoring = true)
    {
        var drive = new FakeAxisTransport { SupportsConcurrentMonitoring = monitoring };
        var axis = new AxisController(drive, new BindAxisSetting { CommunicationTimeoutMs = 2000 }) { PollIntervalMs = 1 };
        await axis.InitializeAsync(default);
        await axis.SetModeAsync(mode, default);
        if (mode is OperationMode.CSP or OperationMode.CSV or OperationMode.CST)
            await axis.WriteCyclicAsync(mode, mode == OperationMode.CSP ? drive.Position : 0, default);
        await axis.EnableAsync(default);
        return (axis, drive);
    }

    [StaFact]
    public void 使能态开关默认关闭并兼容旧设置()
    {
        var plugin = new SetModePlugin();
        var serializer = plugin.CreateSerializer();
        Check(!((SetModeSetting)serializer.CreateDefault()).AllowWhileEnabled);
        var legacy = MessagePackSerializer.Serialize(new Dictionary<string, object>
        {
            ["AxisName"] = "\"Axis1\"",
            ["TimeoutMs"] = 5000,
            ["PollIntervalMs"] = 20,
            ["Mode"] = 3
        }, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
        Check(!((SetModeSetting)serializer.Deserialize(legacy, 1)).AllowWhileEnabled);
        var setting = new SetModeSetting();
        var (_, state) = TestContext.Create(plugin, setting);
        var model = new SetModeViewModel();
        model.AttachSerializer(serializer);
        model.AttachStep(state.Current!.Step);
        Check(!model.AllowWhileEnabled);
        model.AllowWhileEnabled = true;
        model.Flush();
        Check(((SetModeSetting)serializer.Deserialize(state.Current.Step.StepSetting.Setting, 1)).AllowWhileEnabled);
        Check(plugin.Description.Contains("| AllowWhileEnabled |"));
    }
    [Fact]
    public void 生命周期校验遵循显式开关()
    {
        var plugin = new SetModePlugin();
        var setting = new SetModeSetting();
        var (context, state) = TestContext.Create(plugin, setting);
        var (_, bound) = TestContext.Create(new BindAxisPlugin(), new BindAxisSetting());
        var (_, enabled) = TestContext.Create(new EnablePlugin(), new EnableSetting());
        var validation = new StepEditorValidationContext
        {
            Setting = state.Current!.Step.StepSetting.Setting,
            Evaluator = ExpressionEvaluatorFactory.Default,
            ExecutionContext = context,
            SequenceFile = null!,
            CurrentStep = state.Current.Step,
            Block = [bound.Current!.Step, enabled.Current!.Step, state.Current.Step]
        };
        var errors = new List<StepSettingError>();
        AxisLifecycleValidator.Validate(validation, setting, errors);
        Check(errors.Any(e => e.Code == "C402_105"));
        setting.AllowWhileEnabled = true;
        errors.Clear(); AxisLifecycleValidator.Validate(validation, setting, errors);
        Check(!errors.Any(e => e.Code == "C402_105") && errors.Any(e => e.Code == "C402_108"));
    }

    public static IEnumerable<object[]> ModeTransitions =>
        from source in Enum.GetValues<OperationMode>()
        from target in Enum.GetValues<OperationMode>()
        select new object[] { source, target };

    [Theory]
    [MemberData(nameof(ModeTransitions))]
    public async Task 保持使能切换模式(OperationMode source, OperationMode target)
    {
        var (axis, drive) = await Ready(source);
        using var transport = axis.Transport;
        drive.Writes.Clear();
        using var deadline = new CancellationTokenSource(5000);
        await axis.SetModeAsync(target, true, deadline.Token);
        Check(drive.Mode == target && AxisController.DecodeState(drive.Status) == DriveState.OperationEnabled, $"{source} → {target} 状态错误");
        Check(drive.Writes.Where(w => w.Index == 0x6040).All(w => (ObjectCodec.Decode(w.Data, 2, false) & 0x000F) == 0x000F), "切换不应释放使能");
        if (target is OperationMode.PP or OperationMode.CSP) Check(ObjectCodec.Decode(drive.Objects[(0x607A, 0)], 4, true) == drive.Position);
        if (target is OperationMode.PV or OperationMode.CSV) Check(ObjectCodec.Decode(drive.Objects[(0x60FF, 0)], 4, true) == 0);
        if (target is OperationMode.PT or OperationMode.CST) Check(ObjectCodec.Decode(drive.Objects[(0x6071, 0)], 2, true) == 0);
        if (source != target && target is OperationMode.CSP or OperationMode.CSV or OperationMode.CST)
        {
            var count = drive.Targets.Count;
            await axis.WriteCyclicAsync(target, target == OperationMode.CSP ? drive.Position + 1 : 1, deadline.Token);
            Check(drive.Targets.Count == count + 1, "切换后应可继续提交周期目标");
        }
    }
    [Fact]
    public async Task 默认策略仍拒绝使能态切换且不写模式()
    {
        var (axis, drive) = await Ready(); using var transport = axis.Transport;
        drive.Writes.Clear();
        await Throws<InvalidOperationException>(() => axis.SetModeAsync(OperationMode.PV, default));
        Check(drive.Writes.Count == 0 && drive.Mode == OperationMode.PP);
    }
    [Fact]
    public async Task 运动中和启动标记未撤销时拒绝切换()
    {
        var (axis, drive) = await Ready(); using var transport = axis.Transport;
        drive.Writes.Clear(); drive.Velocity = 1;
        await Throws<InvalidOperationException>(() => axis.SetModeAsync(OperationMode.PV, true, default));
        drive.Velocity = 0; drive.Control |= 0x0010;
        await Throws<InvalidOperationException>(() => axis.SetModeAsync(OperationMode.PV, true, default));
        drive.Control = 0x010F;
        await Throws<InvalidOperationException>(() => axis.SetModeAsync(OperationMode.PV, true, default));
        Check(drive.Writes.Count == 0);
    }
    [Fact]
    public async Task 未到位和非零速度转矩目标拒绝切换()
    {
        foreach (var source in new[] { OperationMode.PP, OperationMode.PV, OperationMode.PT, OperationMode.CSP, OperationMode.CSV, OperationMode.CST })
        {
            var (axis, drive) = await Ready(source); using var transport = axis.Transport;
            if (source == OperationMode.PP) drive.Status &= unchecked((ushort)~0x0400);
            else if (source is OperationMode.PV or OperationMode.CSV) drive.Objects[(0x60FF, 0)] = ObjectCodec.Encode(1, 4, true);
            else if (source is OperationMode.PT or OperationMode.CST) drive.Objects[(0x6071, 0)] = ObjectCodec.Encode(1, 2, true);
            else drive.Objects[(0x607A, 0)] = ObjectCodec.Encode(drive.Position + 1, 4, true);
            drive.Writes.Clear();
            await Throws<InvalidOperationException>(() => axis.SetModeAsync(OperationMode.HM, true, default));
            Check(drive.Writes.Count == 0);
        }
    }
    [Fact]
    public async Task 模式显示未确认时不能返回成功()
    {
        var (axis, drive) = await Ready(); using var transport = axis.Transport;
        drive.RejectMode = true;
        using var deadline = new CancellationTokenSource(100);
        await Throws<OperationCanceledException>(() => axis.SetModeAsync(OperationMode.PV, true, deadline.Token));
        Check(drive.Mode == OperationMode.PP);
    }
    [Fact]
    public async Task 切换期间退出使能判为错误()
    {
        var (axis, drive) = await Ready(); using var transport = axis.Transport;
        drive.DropEnableOnModeChange = true;
        await Throws<InvalidOperationException>(() => axis.SetModeAsync(OperationMode.PV, true, default));
    }
    [Fact]
    public async Task SetMode执行器使用显式开关()
    {
        var (axis, drive) = await Ready();
        using var session = new AxisSession(axis, new BindAxisSetting(), default, (_, _) => { });
        var plugin = new SetModePlugin();
        var (context, state) = TestContext.Create(plugin, new SetModeSetting { Mode = OperationMode.PV, AllowWhileEnabled = true, TimeoutMs = 30000 });
        state.Resources[AxisSession.Key("Axis1")] = session;
        var result = await plugin.CreateExecutor().ExecuteAsync(context, default);
        Check(result.StepResult.Status == TestStatus.Completed && drive.Mode == OperationMode.PV);
        Check(AxisController.DecodeState(drive.Status) == DriveState.OperationEnabled);
    }
    [Fact]
    public async Task PP等待到位期间独立执行硬件反馈读取()
    {
        var (axis, drive) = await Ready(); drive.CompletePosition = false;
        using var session = new AxisSession(axis, new BindAxisSetting(), default, (_, _) => { });
        using var deadline = new CancellationTokenSource(10000);
        var motion = session.ExecuteAsync((a, ct) => a.MovePositionAsync(500, 100, 100, 100, false, true, ct), true, 1, deadline.Token);
        try
        {
            await drive.PositionHandshakeCompleted.Task.WaitAsync(deadline.Token);
            Check(!motion.IsCompleted);
            drive.Position = 123;
            var plugin = new ReadFeedbackPlugin();
            var (context, state) = TestContext.Create(plugin, new ReadFeedbackSetting { PositionVariable = "Locals.Position", PollIntervalMs = 30, TimeoutMs = 30000 });
            state.Variables["Locals.Position"] = 0.0;
            state.Resources[AxisSession.Key("Axis1")] = session;
            var result = await plugin.CreateExecutor().ExecuteAsync(context, deadline.Token);
            Check(result.StepResult.Status == TestStatus.Completed);
            Check((double)state.Variables["Locals.Position"]! == 123 && !motion.IsCompleted);
            Check(axis.PollIntervalMs == 1, "监控不得改变运动的轮询间隔");
            await Throws<InvalidOperationException>(() => session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, deadline.Token));
        }
        finally { drive.Status |= 0x0400; }
        await motion;
    }
    [Fact]
    public async Task 多个只读监控可与一个控制流程共存()
    {
        var (axis, _) = await Ready();
        using var session = new AxisSession(axis, new BindAxisSetting(), default, (_, _) => { });
        var release = Signal();
        var motion = session.ExecuteAsync((_, _) => release.Task, true, 7, default);
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => session.ExecuteAsync(async (a, ct) =>
            {
                Check(AxisController.DecodeState(await a.ReadStatusAsync(ct)) == DriveState.OperationEnabled);
            }, false, 50, default)));
            Check(!motion.IsCompleted && axis.PollIntervalMs == 7);
        }
        finally { release.TrySetResult(); await motion; }
    }
    [Fact]
    public async Task 单个监控取消不终止控制流程()
    {
        var (axis, drive) = await Ready();
        using var session = new AxisSession(axis, new BindAxisSetting(), default, (_, _) => { });
        var release = Signal();
        var motion = session.ExecuteAsync((_, _) => release.Task, true, 1, default);
        try
        {
            using var cancel = new CancellationTokenSource(30);
            await Throws<OperationCanceledException>(() => session.ExecuteAsync((_, ct) => Task.Delay(Timeout.Infinite, ct), false, 1, cancel.Token));
            await session.ExecuteAsync(async (a, ct) => { await a.ReadStatusAsync(ct); }, false, 1, default);
            Check(!motion.IsCompleted && !drive.Disposed && (drive.Control & 0xF) == 0xF);
        }
        finally { release.TrySetResult(); await motion; }
    }
    [Fact]
    public async Task 控制失败会取消并发监控并完成一次清理()
    {
        var probe = new ProbeAxisTransport();
        var axis = new AxisController(probe, new BindAxisSetting());
        await axis.InitializeAsync(default);
        using var session = new AxisSession(axis, new BindAxisSetting(), default, (_, _) => { });
        var started = Signal();
        var reader = session.ExecuteAsync(async (_, ct) => { started.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); }, false, 1, default);
        await started.Task;
        await Throws<InvalidOperationException>(() => session.ExecuteAsync((_, _) => Task.FromException(new InvalidOperationException("仿真控制失败")), true, 1, default));
        await Throws<OperationCanceledException>(() => reader);
        await session.CloseAsync();
        Check(probe.DisposeCount == 1);
    }
    [Fact]
    public async Task 单次硬件事务不因并发监控而重入()
    {
        var probe = new ProbeAxisTransport { ReadHandler = async (_, ct) => { await Task.Delay(5, ct); return new byte[] { 0x27, 0 }; } };
        using var transport = new CoordinatedAxisTransport(probe, 5000);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => transport.ReadObjectAsync(0x6041, 0, default)));
        Check(probe.MaximumActive == 1 && probe.Calls == 12);
    }
    [Fact]
    public async Task 排队读取取消后仍可继续使用传输()
    {
        var started = Signal(); var release = Signal();
        var probe = new ProbeAxisTransport { ReadHandler = async (_, _) => { started.TrySetResult(); await release.Task; return new byte[] { 0x27, 0 }; } };
        using var transport = new CoordinatedAxisTransport(probe, 3000);
        var first = transport.ReadObjectAsync(0x6041, 0, default);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            using var cancel = new CancellationTokenSource(30);
            await Throws<OperationCanceledException>(() => transport.ReadObjectAsync(0x6041, 0, cancel.Token));
            Check(probe.Calls == 1);
        }
        finally { release.TrySetResult(); await first; }
        await transport.ReadObjectAsync(0x6041, 0, default);
        Check(probe.Calls == 2 && probe.MaximumActive == 1);
    }
    [Fact]
    public async Task 同步阻塞调用超时后不重入且延后释放()
    {
        using var release = new ManualResetEventSlim();
        var started = Signal();
        var probe = new ProbeAxisTransport
        {
            ReadHandler = (_, _) => { started.TrySetResult(); release.Wait(); return Task.FromResult(new byte[] { 0x27, 0 }); }
        };
        using var transport = new CoordinatedAxisTransport(probe, 200);
        var first = transport.ReadObjectAsync(0x6041, 0, default);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var monitor = transport.ReadObjectAsync(0x6041, 0, default);
            await Throws<TimeoutException>(() => first);
            await Throws<TimeoutException>(() => monitor);
            Check(probe.Calls == 1 && probe.MaximumActive == 1);
            await Throws<InvalidOperationException>(() => transport.WriteObjectAsync(0x6040, 0, [0, 0], default));
            transport.Dispose();
            Check(probe.DisposeCount == 0);
        }
        finally { release.Set(); }
        await probe.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(probe.DisposeCount == 1 && !probe.DisposedDuringCall);
    }
    [Fact]
    public async Task 取消活动阻塞调用不释放底层占用()
    {
        var started = Signal(); var release = Signal();
        var probe = new ProbeAxisTransport { ReadHandler = async (_, _) => { started.TrySetResult(); await release.Task; return new byte[] { 0x27, 0 }; } };
        using var transport = new CoordinatedAxisTransport(probe, 3000);
        using var cancel = new CancellationTokenSource();
        var first = transport.ReadObjectAsync(0x6041, 0, cancel.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancel.Cancel();
            await Throws<OperationCanceledException>(() => first);
            await Throws<InvalidOperationException>(() => transport.ReadObjectAsync(0x6041, 0, default));
            transport.Dispose(); Check(probe.DisposeCount == 0);
        }
        finally { release.TrySetResult(); }
        await probe.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(probe.DisposeCount == 1 && !probe.DisposedDuringCall);
    }
    [Fact]
    public async Task IO完成与释放并发时不遗漏底层释放()
    {
        for (var iteration = 0; iteration < 100; iteration++)
        {
            var started = Signal(); var release = Signal();
            var probe = new ProbeAxisTransport { ReadHandler = async (_, _) => { started.TrySetResult(); await release.Task; return new byte[] { 0x27, 0 }; } };
            using var transport = new CoordinatedAxisTransport(probe, 3000);
            var read = transport.ReadObjectAsync(0x6041, 0, default);
            try
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
                await Task.WhenAll(Task.Run(() => release.TrySetResult()), Task.Run(transport.Dispose));
            }
            finally { release.TrySetResult(); }
            // Close may win the post-I/O publication fence; either returned data or
            // ObjectDisposed is valid, but native ownership must still release once.
            try { await read; }
            catch (ObjectDisposedException) { }
            await probe.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Check(probe.DisposeCount == 1 && !probe.DisposedDuringCall);
        }
    }
    [Fact]
    public async Task CANopen不同节点PDO等待已有SDO事务()
    {
        using var drive = new FakeCanAdapter { Respond = false };
        var connection = CanopenConnectionAccess.For(drive);
        using var firstNode = connection.ReserveNode(1, 3000);
        using var secondNode = connection.ReserveNode(2, 3000);
        using var deadline = new CancellationTokenSource(3000);
        var read = firstNode.ReadObjectAsync(0x6041, 0, deadline.Token);
        while (drive.Sent.IsEmpty) await Task.Delay(1, deadline.Token);
        var send = secondNode.SendPdoAsync(0x202, [0, 0], deadline.Token);
        try
        {
            await Task.Delay(30, deadline.Token);
            Check(drive.Sent.Count == 1 && !send.IsCompleted);
        }
        finally { drive.Incoming.Enqueue(new CanMessage { Id = 0x581, Data = [0x4B, 0x41, 0x60, 0, 0x27, 0, 0, 0] }); }
        await read; await send;
        Check(drive.Sent.Count == 2 && drive.Sent.Last().Id == 0x202);
    }
}
