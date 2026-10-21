using System.Reflection;
using CAN.CANopen;
using CAN.Models;
using CiA402.Core;
using CiA402.Models;
using CiA402.Transport;
using CiA402.UI.ViewModels;
using MessagePack;
using StepEditor.Abstractions;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Models.StepSettings;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CiA402.Tests;

public sealed class ContractTests
{
    internal static IReadOnlyList<IStepPlugin> Plugins { get; } = typeof(BindAxisPlugin).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && typeof(IStepPlugin).IsAssignableFrom(t))
        .Select(t => (IStepPlugin)Activator.CreateInstance(t)!).ToArray();
    private static void Check(bool condition, string message = "断言不成立") => Assert.True(condition, message);
    private static Task<T> Throws<T>(Func<Task> action) where T : Exception => Assert.ThrowsAnyAsync<T>(action);
    private static Task Sync(Action action) { action(); return Task.CompletedTask; }
    private static async Task<(AxisController Axis, FakeAxisTransport Transport)> Axis(OperationMode mode = OperationMode.PP, bool enable = true)
    {
        var transport = new FakeAxisTransport();
        var axis = new AxisController(transport, new BindAxisSetting()) { PollIntervalMs = 1 };
        await axis.InitializeAsync(default);
        await axis.SetModeAsync(mode, default);
        if (enable) await axis.EnableAsync(default);
        return (axis, transport);
    }
    [Fact]
    public void 十八个插件设置及参数文档一致()
    {
        Check(Plugins.Count == 18);
        Check(Plugins.Select(p => p.StepTypeId).Distinct().Count() == 18);
        foreach (var plugin in Plugins)
        {
            var serializer = plugin.CreateSerializer();
            var value = (AxisSetting)serializer.CreateDefault();
            Check(value.GetType().IsDefined(typeof(MessagePackObjectAttribute)));
            var copy = (AxisSetting)serializer.Deserialize(serializer.Serialize(value), 1);
            Check(copy.GetType() == value.GetType() && copy.AxisName == value.AxisName);
            foreach (var field in value.GetType().GetProperties()) Check(plugin.Description.Contains("| " + field.Name + " |"));
            foreach (var heading in new[] { "## 功能", "## 参数", "## 行为", "## 相关插件" }) Check(plugin.Description.Contains(heading));
            Check(plugin.CreateExecutor() != null && plugin.IconPath != null);
            Check(!plugin.Description.Contains("安全停止") && !plugin.Description.Contains("急停") && !plugin.Description.Contains("安全功能"));
        }
    }
    [StaFact]
    public void 十八个独立编辑器及图标匹配()
    {
        var editors = typeof(CiA402.UI.Editors.BindAxisEditorPlugin).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IStepEditorPlugin).IsAssignableFrom(t))
            .Select(t => (IStepEditorPlugin)Activator.CreateInstance(t)!).ToArray();
        Check(editors.Length == 18);
        foreach (var editor in editors) Check(editor.IconPath == Plugins.Single(p => p.StepTypeId == editor.StepTypeId).IconPath);
    }
    [StaFact]
    public void 隐藏控件不能覆盖其他字段()
    {
        var plugin = new BindAxisPlugin();
        var (_, context) = TestContext.Create(plugin, new BindAxisSetting());
        var model = new BindAxisViewModel();
        model.AttachSerializer(plugin.CreateSerializer());
        model.AttachStep(context.Current!.Step);
        model.Transport = TransportKind.EtherCAT;
        Check(!model.IsCANopen && model.IsEtherCAT);
        model.SlaveAddress = "5";
        Check(model.NodeId == "1");
        model.Transport = TransportKind.CANopen;
        model.NodeId = "2";
        model.Flush();
        var saved = (BindAxisSetting)plugin.CreateSerializer().Deserialize(context.Current.Step.StepSetting.Setting, 1);
        Check(saved.NodeId == "2" && saved.SlaveAddress == "5");
    }
    [StaFact]
    public void 编辑器保存及刷新不覆盖外部修改()
    {
        var plugin = new ReadStatePlugin();
        var (_, context) = TestContext.Create(plugin, new ReadStateSetting());
        var step = context.Current!.Step;
        var model = new ReadStateViewModel();
        model.AttachSerializer(plugin.CreateSerializer());
        model.AttachStep(step);
        model.AxisName = "\"Axis2\"";
        model.Flush();
        Check(((ReadStateSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1)).AxisName == "\"Axis2\"");
        step.StepSetting.Setting = plugin.CreateSerializer().Serialize(new ReadStateSetting { AxisName = "\"External\"" });
        model.AttachStep(step);
        Check(model.AxisName == "\"External\"");
    }
    [Fact]
    public void 八种状态不受附加位干扰()
    {
        var states = new[] { (0x00, DriveState.NotReadyToSwitchOn), (0x40, DriveState.SwitchOnDisabled), (0x21, DriveState.ReadyToSwitchOn),
                (0x23, DriveState.SwitchedOn), (0x27, DriveState.OperationEnabled), (0x07, DriveState.QuickStopActive), (0x0F, DriveState.FaultReactionActive), (0x08, DriveState.Fault) };
        foreach (var (word, state) in states)
            for (var bits = 0; bits <= ushort.MaxValue; bits++)
                if ((bits & 0x006F) == 0) Check(AxisController.DecodeState((ushort)(word | bits)) == state);
        Check(AxisController.DecodeState(0x25) == DriveState.Unknown);
    }
    [Fact]
    public void 有符号边界及小端编码()
    {
        foreach (var bytes in new[] { 1, 2, 4 })
            foreach (var value in new[] { -(1L << (bytes * 8 - 1)), -1, 0, (1L << (bytes * 8 - 1)) - 1 })
                Check(ObjectCodec.Decode(ObjectCodec.Encode(value, bytes, true), bytes, true) == value);
        Check(ObjectCodec.Encode(0x12345678, 4, false).SequenceEqual(new byte[] { 0x78, 0x56, 0x34, 0x12 }));
    }
    [Fact]
    public async Task 错误长度与溢出拒绝()
    {
        await Throws<InvalidOperationException>(() => Sync(() => ObjectCodec.Decode([1], 2, false)));
        await Throws<ArgumentOutOfRangeException>(() => Sync(() => ObjectCodec.Encode(32768, 2, true)));
    }
    [Fact]
    public async Task PDO布局重复和超长拒绝()
    {
        Check(PdoLayout.Parse("0x6040:0:16,0x607A:0:32").Count == 2);
        await Throws<ArgumentException>(() => Sync(() => PdoLayout.Parse("0x6040:0:16,0x6040:0:16")));
        await Throws<ArgumentException>(() => Sync(() => PdoLayout.Parse("0x6040:0:16,0x607A:0:32,0x60FF:0:32")));
    }
    [Fact]
    public async Task 数值表达式拒绝字符串与小数()
    {
        Check(SettingValidation.Integer(10, "目标", 0, 20) == 10);
        await Throws<ArgumentException>(() => Sync(() => SettingValidation.Integer("10", "目标", 0, 20)));
        await Throws<ArgumentException>(() => Sync(() => SettingValidation.Integer(1.5, "目标", 0, 20)));
    }
    [Fact]
    public void 无效通信与诊断参数拒绝()
    {
        Check(SettingValidation.Validate(new BindAxisSetting { DiagnosticBytes = 3, CommunicationTimeoutMs = 0 }).Count >= 2);
        Check(SettingValidation.Validate(new BindAxisSetting { DiagnosticMask = 1, DiagnosticIndex = 0 }).Count > 0);
    }
    [Fact]
    public async Task 按反馈逐级使能()
    {
        var (axis, fake) = await Axis();
        Check(AxisController.DecodeState(fake.Status) == DriveState.OperationEnabled);
        var words = fake.Writes.Where(w => w.Index == 0x6040).Select(w => ObjectCodec.Decode(w.Data, 2, false)).ToArray();
        Check(words.Contains(6) && words.Contains(7) && words.Contains(15));
        var count = fake.Writes.Count; await axis.EnableAsync(default); Check(count == fake.Writes.Count);
    }
    [Fact]
    public async Task 故障不自动使能()
    {
        var (axis, fake) = await Axis(enable: false); fake.Status = 8;
        await Throws<InvalidOperationException>(() => axis.EnableAsync(default));
    }
    [Fact]
    public async Task 不自动恢复QuickStop()
    {
        var (axis, fake) = await Axis(enable: false); fake.Status = 7;
        await Throws<InvalidOperationException>(() => axis.EnableAsync(default));
    }
    [Fact]
    public async Task 复位上升沿及撤销()
    {
        var (axis, fake) = await Axis(enable: false); fake.Status = 8; fake.Writes.Clear();
        await axis.ResetFaultAsync(default);
        var words = fake.Writes.Where(w => w.Index == 0x6040).Select(w => ObjectCodec.Decode(w.Data, 2, false)).ToArray();
        Check(words.SequenceEqual(new long[] { 0, 128, 0 }));
        Check(AxisController.DecodeState(fake.Status) == DriveState.SwitchOnDisabled);
    }
    [Fact]
    public async Task 运行中禁止切换模式()
    {
        var (axis, _) = await Axis(); await Throws<InvalidOperationException>(() => axis.SetModeAsync(OperationMode.PV, default));
    }
    [Fact]
    public async Task 模式显示不确认时可取消()
    {
        var (axis, fake) = await Axis(enable: false); fake.RejectMode = true;
        using var deadline = new CancellationTokenSource(50);
        await Throws<OperationCanceledException>(() => axis.SetModeAsync(OperationMode.PV, deadline.Token));
    }
    [Fact]
    public async Task PP本次目标握手并到位()
    {
        var (axis, fake) = await Axis();
        await axis.MovePositionAsync(-1234, 100, 200, 300, false, true, default);
        Check(fake.Position == -1234 && (fake.Control & 0x10) == 0);
    }
    [Fact]
    public async Task PV写入有符号速度()
    {
        var (axis, fake) = await Axis(OperationMode.PV);
        await axis.RunVelocityAsync(-42, 100, 100, default);
        Check(ObjectCodec.Decode(fake.Objects[(0x60FF, 0)], 4, true) == -42);
    }
    [Fact]
    public async Task PT写入转矩及斜率()
    {
        var (axis, fake) = await Axis(OperationMode.PT);
        await axis.RunTorqueAsync(-100, 250, default);
        Check(ObjectCodec.Decode(fake.Objects[(0x6071, 0)], 2, true) == -100);
        Check(ObjectCodec.Decode(fake.Objects[(0x6087, 0)], 4, false) == 250);
    }
    [Fact]
    public async Task HM启动与完成()
    {
        var (axis, fake) = await Axis(OperationMode.HM);
        await axis.HomeAsync(35, 100, 20, 100, default);
        Check((fake.Control & 0x10) == 0);
    }
    [Fact]
    public async Task HM错误及时退出()
    {
        var (axis, fake) = await Axis(OperationMode.HM); fake.HomingError = true;
        await Throws<InvalidOperationException>(() => axis.HomeAsync(35, 100, 20, 100, default));
    }
    [Theory]
    [InlineData(OperationMode.CSP)]
    [InlineData(OperationMode.CSV)]
    [InlineData(OperationMode.CST)]
    public async Task 周期模式初始目标单次提交和释放(OperationMode mode)
    {
        var (axis, fake) = await Axis(mode, false);
        await Throws<InvalidOperationException>(() => axis.EnableAsync(default));
        await Throws<InvalidOperationException>(() => axis.WriteCyclicAsync(mode, 9999, default));
        await axis.WriteCyclicAsync(mode, mode == OperationMode.CSP ? fake.Position : 0, default);
        await axis.EnableAsync(default);
        var before = fake.Targets.Count;
        await axis.WriteCyclicAsync(mode, -10, default); Check(fake.Targets.Count == before + 1);
        await axis.DisableAsync(default); Check((fake.Targets.Last().Control & 0xF) == 0);
    }
    [Fact]
    public async Task 停止运动等待零速但不恢复运行()
    {
        var (axis, fake) = await Axis(OperationMode.PV); fake.Velocity = 50;
        await axis.StopAsync(default); Check(fake.Velocity == 0 && AxisController.DecodeState(fake.Status) == DriveState.QuickStopActive);
    }
    [Fact]
    public async Task 硬件诊断阻止运动()
    {
        var fake = new FakeAxisTransport(); fake.Objects[(0x2000, 1)] = [1, 0];
        var axis = new AxisController(fake, new BindAxisSetting { DiagnosticIndex = 0x2000, DiagnosticSubIndex = 1, DiagnosticMask = 1, DiagnosticActiveValue = 1 });
        await axis.InitializeAsync(default);
        await Throws<InvalidOperationException>(() => axis.EnableAsync(default));
    }
    [Fact]
    public async Task 取消后停止运动并释放节点()
    {
        var (axis, fake) = await Axis(enable: false);
        using var cancel = new CancellationTokenSource();
        using var session = new AxisSession(axis, new BindAxisSetting(), cancel.Token, (_, _) => { });
        await session.ExecuteAsync((a, ct) => a.EnableAsync(ct), true, 1, default);
        cancel.Cancel(); await session.CloseAsync();
        Check(fake.Disposed && (fake.Control & 0xF) == 0);
    }
    [Fact]
    public async Task 只读会话释放不写控制字()
    {
        var (axis, fake) = await Axis(enable: false); var count = fake.Writes.Count;
        using var session = new AxisSession(axis, new BindAxisSetting(), default, (_, _) => { });
        await session.ExecuteAsync(async (a, ct) => { await a.ReadStatusAsync(ct); }, false, 1, default);
        await session.CloseAsync(); Check(count == fake.Writes.Count);
    }
    [Fact]
    public async Task 同轴并发访问拒绝()
    {
        var (axis, _) = await Axis(enable: false);
        using var session = new AxisSession(axis, new BindAxisSetting(), default, (_, _) => { });
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = session.ExecuteAsync((_, _) => release.Task, false, 1, default);
        await Throws<InvalidOperationException>(() => session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default));
        release.SetResult(); await first;
    }
    [Fact]
    public async Task CANopen快速SDO与节点独占()
    {
        using var fake = new FakeCanAdapter(); fake.Object(0x6041, 0, 0x40, 2);
        var access = CanopenConnectionAccess.For(fake);
        using var node = access.ReserveNode(1, 1000);
        await Throws<InvalidOperationException>(() => Sync(() => access.ReserveNode(1, 1000)));
        Check(ObjectCodec.Decode(await node.ReadObjectAsync(0x6041, 0, default), 2, false) == 0x40);
        await node.WriteObjectAsync(0x6040, 0, [6, 0], default);
        Check(fake.Objects[(0x6040, 0)].SequenceEqual(new byte[] { 6, 0 }));
    }
    [Fact]
    public async Task SDO读取保留其他TPDO()
    {
        using var fake = new FakeCanAdapter(); fake.Object(0x6041, 0, 0x40, 2);
        fake.Incoming.Enqueue(new CanMessage { Id = 0x181, Data = [0x27, 0] });
        using var node = CanopenConnectionAccess.For(fake).ReserveNode(1, 1000);
        await node.ReadObjectAsync(0x6041, 0, default);
        Check((await node.ReceivePdoAsync(0x181, default))?.Frame.Data[0] == 0x27);
    }
    [Fact]
    public async Task CANopenPDO映射验证与打包()
    {
        using var fake = new FakeCanAdapter(); fake.Pdo(1, false, 0x201, "0x6040:0:16,0x607A:0:32");
        using var transport = new CanopenAxisTransport(CanopenConnectionAccess.For(fake).ReserveNode(1, 1000),
            new BindAxisSetting { CspPdoNumber = 1, CspPdoLayout = "0x6040:0:16,0x607A:0:32" });
        await transport.InitializeAsync(default); await transport.WriteTargetAsync(OperationMode.CSP, -1, 15, default);
        Check(fake.Sent.Last().Data.SequenceEqual(new byte[] { 15, 0, 255, 255, 255, 255 }));
    }
    [Fact]
    public async Task CANopen映射不一致拒绝()
    {
        using var fake = new FakeCanAdapter(); fake.Pdo(1, false, 0x201, "0x60FF:0:32");
        using var transport = new CanopenAxisTransport(CanopenConnectionAccess.For(fake).ReserveNode(1, 1000), new BindAxisSetting { CspPdoNumber = 1 });
        await Throws<InvalidOperationException>(() => transport.InitializeAsync(default));
    }
    [Fact]
    public async Task 反馈TPDO解码与未映射输出拒绝()
    {
        using var fake = new FakeCanAdapter(); fake.Pdo(1, true, 0x181, "0x6041:0:16,0x6064:0:32,0x6077:0:16");
        using var transport = new CanopenAxisTransport(CanopenConnectionAccess.For(fake).ReserveNode(1, 1000), new BindAxisSetting { FeedbackPdoNumber = 1 });
        await transport.InitializeAsync(default);
        fake.Incoming.Enqueue(new CanMessage { Id = 0x181, Data = [0x27, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF], TimestampNs = 1 });
        var feedback = await transport.ReadFeedbackAsync(true, false, true, default);
        Check(feedback.Position == -1 && feedback.Torque == -2);
        await Throws<InvalidOperationException>(() => transport.ReadFeedbackAsync(false, true, false, default));
    }
    [Fact]
    public async Task 取消原始接收后连接仍可使用()
    {
        using var fake = new FakeCanAdapter();
        var access = CanopenConnectionAccess.For(fake);
        using var cancel = new CancellationTokenSource(10);
        await Throws<OperationCanceledException>(() => access.ReceiveAsync(0x181, 1000, cancel.Token));
        await access.SendAsync(0x80, [], 1000, default);
        Check(fake.Sent.Last().Id == 0x80);
    }
    [Fact]
    public async Task 中断SDO后隔离迟到响应()
    {
        using var fake = new FakeCanAdapter { Respond = false };
        using var node = CanopenConnectionAccess.For(fake).ReserveNode(1, 50);
        await Throws<TimeoutException>(() => node.ReadObjectAsync(0x6041, 0, default));
        fake.Respond = true; fake.Object(0x6041, 0, 0x40, 2);
        var sent = fake.Sent.Count;
        await Throws<InvalidOperationException>(() => node.ReadObjectAsync(0x6041, 0, default));
        Check(fake.Sent.Count == sent);
    }
    [Fact]
    public async Task 不完整SDO不能补零解释为状态()
    {
        using var fake = new FakeCanAdapter(); fake.Object(0x6041, 0, 0x40, 2);
        fake.Incoming.Enqueue(new CanMessage { Id = 0x581, Data = [0x4B, 0x41, 0x60, 0] });
        using var node = CanopenConnectionAccess.For(fake).ReserveNode(1, 1000);
        await Throws<InvalidOperationException>(() => node.ReadObjectAsync(0x6041, 0, default));
    }
    [Fact]
    public async Task 过期队列反馈不会当作有效结果()
    {
        using var fake = new FakeCanAdapter(); fake.Pdo(1, true, 0x181, "0x6041:0:16,0x6064:0:32,0x6077:0:16"); fake.Object(0x6041, 0, 0x40, 2);
        using var node = CanopenConnectionAccess.For(fake).ReserveNode(1, 100);
        using var transport = new CanopenAxisTransport(node, new BindAxisSetting { FeedbackPdoNumber = 1, FeedbackMaxAgeMs = 1, CommunicationTimeoutMs = 100 });
        await transport.InitializeAsync(default);
        fake.Incoming.Enqueue(new CanMessage { Id = 0x181, Data = [0x27, 0, 0, 0, 0, 0, 0, 0], TimestampNs = 1 });
        await node.ReadObjectAsync(0x6041, 0, default);
        await Task.Delay(20);
        await Throws<TimeoutException>(() => transport.ReadFeedbackAsync(true, false, false, default));
    }
    [Fact]
    public async Task 活动操作退出后完成延后释放()
    {
        var (axis, fake) = await Axis(enable: false);
        using var session = new AxisSession(axis, new BindAxisSetting { CommunicationTimeoutMs = 10 }, default, (_, _) => { });
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = session.ExecuteAsync((_, _) => release.Task, true, 1, default);
        await session.CloseAsync(); Check(!fake.Disposed);
        release.SetResult(); await Throws<OperationCanceledException>(() => operation);
        Check(fake.Disposed);
    }
    [Fact]
    public async Task CSP使能前重新核对当前位置()
    {
        var (axis, fake) = await Axis(OperationMode.CSP, false);
        await axis.WriteCyclicAsync(OperationMode.CSP, fake.Position, default);
        fake.Position++;
        await Throws<InvalidOperationException>(() => axis.EnableAsync(default));
    }
    [Fact]
    public async Task 输出只允许已声明的完整变量路径()
    {
        var (context, state) = TestContext.Create(new ReadStatePlugin(), new ReadStateSetting());
        await Throws<ArgumentException>(() => Sync(() => SettingValidation.ValidateOutput(context, "Value", typeof(double))));
        await Throws<ArgumentException>(() => Sync(() => SettingValidation.ValidateOutput(context, "Locals.Missing", typeof(double))));
        state.Variables["Locals.Value"] = 0.0;
        SettingValidation.ValidateOutput(context, "Locals.Value", typeof(double));
        SettingValidation.Output(context, "Locals.Value", 42.0); Check((double)state.Variables["Locals.Value"]! == 42);
    }
    [Fact]
    public async Task 取消执行结果为Aborted()
    {
        var plugin = new ReadStatePlugin(); var (context, _) = TestContext.Create(plugin, new ReadStateSetting());
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Check((await plugin.CreateExecutor().ExecuteAsync(context, cancel.Token)).StepResult.Status == TestStatus.Aborted);
    }
    [Fact]
    public async Task 未绑定轴返回中文Error()
    {
        var plugin = new ReadStatePlugin(); var (context, _) = TestContext.Create(plugin, new ReadStateSetting { TimeoutMs = 30000 });
        var result = await plugin.CreateExecutor().ExecuteAsync(context, default);
        Check(result.StepResult.Status == TestStatus.Error && result.StepResult.Error?.Message.Contains("不存在") == true);
    }
    [Fact]
    public async Task 无EtherCAT工厂不能伪装成功()
    {
        var plugin = new BindAxisPlugin(); var (context, _) = TestContext.Create(plugin, new BindAxisSetting { Transport = TransportKind.EtherCAT, TimeoutMs = 30000 });
        var result = await plugin.CreateExecutor().ExecuteAsync(context, default);
        Check(result.StepResult.Status == TestStatus.Error && result.StepResult.Error?.Message.Contains("EtherCAT") == true);
    }
    [Fact]
    public async Task 执行器超时结果为Error而非Aborted()
    {
        var plugin = new EnablePlugin(); var (context, state) = TestContext.Create(plugin, new EnableSetting { TimeoutMs = 100, PollIntervalMs = 1 });
        var (axis, fake) = await Axis(enable: false); fake.Freeze = true;
        using var session = new AxisSession(axis, new BindAxisSetting(), default, (_, _) => { });
        state.Resources[AxisSession.Key("Axis1")] = session;
        var result = await plugin.CreateExecutor().ExecuteAsync(context, default);
        Check(result.StepResult.Status == TestStatus.Error && result.StepResult.Error?.Message.Contains("超时") == true);
    }
}
