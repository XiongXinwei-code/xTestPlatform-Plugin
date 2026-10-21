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

namespace CiA402.Tests;

internal static class Program
{
    private static int _passed;
    private static int _failed;
    [STAThread]
    private static int Main()
    {
        Run().GetAwaiter().GetResult();
        Console.WriteLine($"CiA402 仿真/契约测试：通过 {_passed}，失败 {_failed}。未执行硬件联调。");
        return _failed == 0 ? 0 : 1;
    }
    private static void Check(bool condition, string message = "断言不成立") { if (!condition) throw new InvalidOperationException(message); }
    private static async Task Test(string name, Func<Task> action)
    {
        try { await action(); _passed++; Console.WriteLine($"通过：{name}"); }
        catch (Exception ex) { _failed++; Console.WriteLine($"失败：{name}：{ex}"); }
    }
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new InvalidOperationException($"预期 {typeof(T).Name}");
    }
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
    private static async Task Run()
    {
        await Test("十八个插件、设置及参数文档一致", () => Sync(() =>
        {
            Check(StepCatalog.All.Count == 18);
            Check(StepCatalog.All.Select(p => p.StepTypeId).Distinct().Count() == 18);
            foreach (var plugin in StepCatalog.All)
            {
                Check(plugin.SettingType.IsDefined(typeof(MessagePackObjectAttribute)));
                var serializer = plugin.CreateSerializer();
                var value = (AxisSetting)serializer.CreateDefault();
                var copy = (AxisSetting)serializer.Deserialize(serializer.Serialize(value), 1);
                Check(copy.GetType() == value.GetType() && copy.AxisName == value.AxisName);
                foreach (var field in SettingFields.Get(value.GetType())) Check(plugin.Description.Contains("| " + field.Property.Name + " |"));
                foreach (var heading in new[] { "## 功能", "## 参数", "## 行为", "## 相关插件" }) Check(plugin.Description.Contains(heading));
                Check(plugin.CreateExecutor() != null && plugin.IconPath != null);
                Check(!plugin.Description.Contains("安全停止") && !plugin.Description.Contains("急停") && !plugin.Description.Contains("安全功能"));
            }
        }));
        await Test("十八个独立编辑器及图标匹配", () => Sync(() =>
        {
            var editors = typeof(CiA402.UI.Editors.BindAxisEditorPlugin).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(IStepEditorPlugin).IsAssignableFrom(t))
                .Select(t => (IStepEditorPlugin)Activator.CreateInstance(t)!).ToArray();
            Check(editors.Length == 18);
            foreach (var editor in editors) Check(editor.IconPath == StepCatalog.ForId(editor.StepTypeId).IconPath);
        }));
        await Test("隐藏控件不能覆盖其他字段", () => Sync(() =>
        {
            var setting = new BindAxisSetting();
            var field = new FieldViewModel(setting, SettingFields.Get(setting.GetType()).Single(f => f.Property.Name == nameof(setting.NodeId)));
            field.EnumValue = null; field.BooleanValue = true; field.TextValue = "";
            Check(setting.NodeId == "1");
            field.ExpressionValue = "2"; Check(setting.NodeId == "2");
        }));
        await Test("编辑器保存及刷新不覆盖外部修改", () => Sync(() =>
        {
            var plugin = new ReadStatePlugin();
            var (_, context) = TestContext.Create(plugin, new ReadStateSetting());
            var step = context.Current!.Step;
            var model = new AxisEditorViewModel();
            model.Attach(plugin, step);
            model.Fields.Single(f => f.Label == "轴会话名").ExpressionValue = "\"Axis2\"";
            model.Flush();
            Check(((ReadStateSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1)).AxisName == "\"Axis2\"");
            step.StepSetting.Setting = plugin.CreateSerializer().Serialize(new ReadStateSetting { AxisName = "\"External\"" });
            model.Attach(plugin, step);
            Check(model.Fields.Single(f => f.Label == "轴会话名").ExpressionValue == "\"External\"");
        }));
        await Test("八种状态不受附加位干扰", () => Sync(() =>
        {
            var states = new[] { (0x00, DriveState.NotReadyToSwitchOn), (0x40, DriveState.SwitchOnDisabled), (0x21, DriveState.ReadyToSwitchOn),
                (0x23, DriveState.SwitchedOn), (0x27, DriveState.OperationEnabled), (0x07, DriveState.QuickStopActive), (0x0F, DriveState.FaultReactionActive), (0x08, DriveState.Fault) };
            foreach (var (word, state) in states)
                for (var bits = 0; bits <= ushort.MaxValue; bits++)
                    if ((bits & 0x006F) == 0) Check(AxisController.DecodeState((ushort)(word | bits)) == state);
            Check(AxisController.DecodeState(0x25) == DriveState.Unknown);
        }));
        await Test("有符号边界及小端编码", () => Sync(() =>
        {
            foreach (var bytes in new[] { 1, 2, 4 })
                foreach (var value in new[] { -(1L << (bytes * 8 - 1)), -1, 0, (1L << (bytes * 8 - 1)) - 1 })
                    Check(ObjectCodec.Decode(ObjectCodec.Encode(value, bytes, true), bytes, true) == value);
            Check(ObjectCodec.Encode(0x12345678, 4, false).SequenceEqual(new byte[] { 0x78, 0x56, 0x34, 0x12 }));
        }));
        await Test("错误长度与溢出拒绝", async () =>
        {
            await Throws<InvalidOperationException>(() => Sync(() => ObjectCodec.Decode([1], 2, false)));
            await Throws<ArgumentOutOfRangeException>(() => Sync(() => ObjectCodec.Encode(32768, 2, true)));
        });
        await Test("PDO 布局重复和超长拒绝", async () =>
        {
            Check(PdoLayout.Parse("0x6040:0:16,0x607A:0:32").Count == 2);
            await Throws<ArgumentException>(() => Sync(() => PdoLayout.Parse("0x6040:0:16,0x6040:0:16")));
            await Throws<ArgumentException>(() => Sync(() => PdoLayout.Parse("0x6040:0:16,0x607A:0:32,0x60FF:0:32")));
        });
        await Test("数值表达式拒绝字符串与小数", async () =>
        {
            Check(SettingValidation.Integer(10, "目标", 0, 20) == 10);
            await Throws<ArgumentException>(() => Sync(() => SettingValidation.Integer("10", "目标", 0, 20)));
            await Throws<ArgumentException>(() => Sync(() => SettingValidation.Integer(1.5, "目标", 0, 20)));
        });
        await Test("无效通信与诊断参数拒绝", () => Sync(() =>
        {
            Check(SettingValidation.Validate(new BindAxisSetting { DiagnosticBytes = 3, CommunicationTimeoutMs = 0 }).Count >= 2);
            Check(SettingValidation.Validate(new BindAxisSetting { DiagnosticMask = 1, DiagnosticIndex = 0 }).Count > 0);
        }));
        await Test("按反馈逐级使能", async () =>
        {
            var (axis, fake) = await Axis();
            Check(AxisController.DecodeState(fake.Status) == DriveState.OperationEnabled);
            var words = fake.Writes.Where(w => w.Index == 0x6040).Select(w => ObjectCodec.Decode(w.Data, 2, false)).ToArray();
            Check(words.Contains(6) && words.Contains(7) && words.Contains(15));
            var count = fake.Writes.Count; await axis.EnableAsync(default); Check(count == fake.Writes.Count);
        });
        await Test("故障不自动使能", async () =>
        {
            var (axis, fake) = await Axis(enable: false); fake.Status = 8;
            await Throws<InvalidOperationException>(() => axis.EnableAsync(default));
        });
        await Test("不自动恢复 QuickStop", async () =>
        {
            var (axis, fake) = await Axis(enable: false); fake.Status = 7;
            await Throws<InvalidOperationException>(() => axis.EnableAsync(default));
        });
        await Test("复位上升沿及撤销", async () =>
        {
            var (axis, fake) = await Axis(enable: false); fake.Status = 8; fake.Writes.Clear();
            await axis.ResetFaultAsync(default);
            var words = fake.Writes.Where(w => w.Index == 0x6040).Select(w => ObjectCodec.Decode(w.Data, 2, false)).ToArray();
            Check(words.SequenceEqual(new long[] { 0, 128, 0 }));
            Check(AxisController.DecodeState(fake.Status) == DriveState.SwitchOnDisabled);
        });
        await Test("运行中禁止切换模式", async () =>
        {
            var (axis, _) = await Axis(); await Throws<InvalidOperationException>(() => axis.SetModeAsync(OperationMode.PV, default));
        });
        await Test("模式显示不确认时可取消", async () =>
        {
            var (axis, fake) = await Axis(enable: false); fake.RejectMode = true;
            using var deadline = new CancellationTokenSource(50);
            await Throws<OperationCanceledException>(() => axis.SetModeAsync(OperationMode.PV, deadline.Token));
        });
        await Test("PP 本次目标握手并到位", async () =>
        {
            var (axis, fake) = await Axis();
            await axis.MovePositionAsync(-1234, 100, 200, 300, false, true, default);
            Check(fake.Position == -1234 && (fake.Control & 0x10) == 0);
        });
        await Test("PV 写入有符号速度", async () =>
        {
            var (axis, fake) = await Axis(OperationMode.PV);
            await axis.RunVelocityAsync(-42, 100, 100, default);
            Check(ObjectCodec.Decode(fake.Objects[(0x60FF, 0)], 4, true) == -42);
        });
        await Test("PT 写入转矩及斜率", async () =>
        {
            var (axis, fake) = await Axis(OperationMode.PT);
            await axis.RunTorqueAsync(-100, 250, default);
            Check(ObjectCodec.Decode(fake.Objects[(0x6071, 0)], 2, true) == -100);
            Check(ObjectCodec.Decode(fake.Objects[(0x6087, 0)], 4, false) == 250);
        });
        await Test("HM 启动与完成", async () =>
        {
            var (axis, fake) = await Axis(OperationMode.HM);
            await axis.HomeAsync(35, 100, 20, 100, default);
            Check((fake.Control & 0x10) == 0);
        });
        await Test("HM 错误及时退出", async () =>
        {
            var (axis, fake) = await Axis(OperationMode.HM); fake.HomingError = true;
            await Throws<InvalidOperationException>(() => axis.HomeAsync(35, 100, 20, 100, default));
        });
        foreach (var mode in new[] { OperationMode.CSP, OperationMode.CSV, OperationMode.CST })
        {
            await Test($"{mode} 初始目标、单次提交和释放", async () =>
            {
                var (axis, fake) = await Axis(mode, false);
                await Throws<InvalidOperationException>(() => axis.EnableAsync(default));
                await Throws<InvalidOperationException>(() => axis.WriteCyclicAsync(mode, 9999, default));
                await axis.WriteCyclicAsync(mode, mode == OperationMode.CSP ? fake.Position : 0, default);
                await axis.EnableAsync(default);
                var before = fake.Targets.Count;
                await axis.WriteCyclicAsync(mode, -10, default); Check(fake.Targets.Count == before + 1);
                await axis.DisableAsync(default); Check((fake.Targets.Last().Control & 0xF) == 0);
            });
        }
        await Test("停止运动等待零速但不恢复运行", async () =>
        {
            var (axis, fake) = await Axis(OperationMode.PV); fake.Velocity = 50;
            await axis.StopAsync(default); Check(fake.Velocity == 0 && AxisController.DecodeState(fake.Status) == DriveState.QuickStopActive);
        });
        await Test("硬件诊断阻止运动", async () =>
        {
            var fake = new FakeAxisTransport(); fake.Objects[(0x2000, 1)] = [1, 0];
            var axis = new AxisController(fake, new BindAxisSetting { DiagnosticIndex = 0x2000, DiagnosticSubIndex = 1, DiagnosticMask = 1, DiagnosticActiveValue = 1 });
            await axis.InitializeAsync(default);
            await Throws<InvalidOperationException>(() => axis.EnableAsync(default));
        });
        await Test("取消后停止运动并释放节点", async () =>
        {
            var (axis, fake) = await Axis(enable: false);
            using var cancel = new CancellationTokenSource();
            using var session = new AxisSession(axis, new BindAxisSetting(), cancel.Token, (_, _) => { });
            await session.ExecuteAsync((a, ct) => a.EnableAsync(ct), true, 1, default);
            cancel.Cancel(); await session.CloseAsync();
            Check(fake.Disposed && (fake.Control & 0xF) == 0);
        });
        await Test("只读会话释放不写控制字", async () =>
        {
            var (axis, fake) = await Axis(enable: false); var count = fake.Writes.Count;
            using var session = new AxisSession(axis, new BindAxisSetting(), default, (_, _) => { });
            await session.ExecuteAsync(async (a, ct) => { await a.ReadStatusAsync(ct); }, false, 1, default);
            await session.CloseAsync(); Check(count == fake.Writes.Count);
        });
        await Test("同轴并发访问拒绝", async () =>
        {
            var (axis, _) = await Axis(enable: false);
            using var session = new AxisSession(axis, new BindAxisSetting(), default, (_, _) => { });
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = session.ExecuteAsync((_, _) => release.Task, false, 1, default);
            await Throws<InvalidOperationException>(() => session.ExecuteAsync((_, _) => Task.CompletedTask, true, 1, default));
            release.SetResult(); await first;
        });
        await Test("CANopen 快速 SDO 与节点独占", async () =>
        {
            using var fake = new FakeCanAdapter(); fake.Object(0x6041, 0, 0x40, 2);
            var access = CanopenConnectionAccess.For(fake);
            using var node = access.ReserveNode(1, 1000);
            await Throws<InvalidOperationException>(() => Sync(() => access.ReserveNode(1, 1000)));
            Check(ObjectCodec.Decode(await node.ReadObjectAsync(0x6041, 0, default), 2, false) == 0x40);
            await node.WriteObjectAsync(0x6040, 0, [6, 0], default);
            Check(fake.Objects[(0x6040, 0)].SequenceEqual(new byte[] { 6, 0 }));
        });
        await Test("SDO 读取保留其他 TPDO", async () =>
        {
            using var fake = new FakeCanAdapter(); fake.Object(0x6041, 0, 0x40, 2);
            fake.Incoming.Enqueue(new CanMessage { Id = 0x181, Data = [0x27, 0] });
            using var node = CanopenConnectionAccess.For(fake).ReserveNode(1, 1000);
            await node.ReadObjectAsync(0x6041, 0, default);
            Check((await node.ReceivePdoAsync(0x181, default))?.Frame.Data[0] == 0x27);
        });
        await Test("CANopen PDO 映射验证与打包", async () =>
        {
            using var fake = new FakeCanAdapter(); fake.Pdo(1, false, 0x201, "0x6040:0:16,0x607A:0:32");
            using var transport = new CanopenAxisTransport(CanopenConnectionAccess.For(fake).ReserveNode(1, 1000),
                new BindAxisSetting { CspPdoNumber = 1, CspPdoLayout = "0x6040:0:16,0x607A:0:32" });
            await transport.InitializeAsync(default); await transport.WriteTargetAsync(OperationMode.CSP, -1, 15, default);
            Check(fake.Sent.Last().Data.SequenceEqual(new byte[] { 15, 0, 255, 255, 255, 255 }));
        });
        await Test("CANopen 映射不一致拒绝", async () =>
        {
            using var fake = new FakeCanAdapter(); fake.Pdo(1, false, 0x201, "0x60FF:0:32");
            using var transport = new CanopenAxisTransport(CanopenConnectionAccess.For(fake).ReserveNode(1, 1000), new BindAxisSetting { CspPdoNumber = 1 });
            await Throws<InvalidOperationException>(() => transport.InitializeAsync(default));
        });
        await Test("反馈 TPDO 解码与未映射输出拒绝", async () =>
        {
            using var fake = new FakeCanAdapter(); fake.Pdo(1, true, 0x181, "0x6041:0:16,0x6064:0:32,0x6077:0:16");
            using var transport = new CanopenAxisTransport(CanopenConnectionAccess.For(fake).ReserveNode(1, 1000), new BindAxisSetting { FeedbackPdoNumber = 1 });
            await transport.InitializeAsync(default);
            fake.Incoming.Enqueue(new CanMessage { Id = 0x181, Data = [0x27, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF], TimestampNs = 1 });
            var feedback = await transport.ReadFeedbackAsync(true, false, true, default);
            Check(feedback.Position == -1 && feedback.Torque == -2);
            await Throws<InvalidOperationException>(() => transport.ReadFeedbackAsync(false, true, false, default));
        });
        await Test("取消原始接收后连接仍可使用", async () =>
        {
            using var fake = new FakeCanAdapter();
            var access = CanopenConnectionAccess.For(fake);
            using var cancel = new CancellationTokenSource(10);
            await Throws<OperationCanceledException>(() => access.ReceiveAsync(0x181, 1000, cancel.Token));
            await access.SendAsync(0x80, [], 1000, default);
            Check(fake.Sent.Last().Id == 0x80);
        });
        await Test("中断 SDO 后隔离迟到响应", async () =>
        {
            using var fake = new FakeCanAdapter { Respond = false };
            using var node = CanopenConnectionAccess.For(fake).ReserveNode(1, 50);
            await Throws<TimeoutException>(() => node.ReadObjectAsync(0x6041, 0, default));
            fake.Respond = true; fake.Object(0x6041, 0, 0x40, 2);
            var sent = fake.Sent.Count;
            await Throws<InvalidOperationException>(() => node.ReadObjectAsync(0x6041, 0, default));
            Check(fake.Sent.Count == sent);
        });
        await Test("不完整 SDO 不能补零解释为状态", async () =>
        {
            using var fake = new FakeCanAdapter(); fake.Object(0x6041, 0, 0x40, 2);
            fake.Incoming.Enqueue(new CanMessage { Id = 0x581, Data = [0x4B, 0x41, 0x60, 0] });
            using var node = CanopenConnectionAccess.For(fake).ReserveNode(1, 1000);
            await Throws<InvalidOperationException>(() => node.ReadObjectAsync(0x6041, 0, default));
        });
        await Test("过期队列反馈不会当作有效结果", async () =>
        {
            using var fake = new FakeCanAdapter(); fake.Pdo(1, true, 0x181, "0x6041:0:16,0x6064:0:32,0x6077:0:16"); fake.Object(0x6041, 0, 0x40, 2);
            using var node = CanopenConnectionAccess.For(fake).ReserveNode(1, 100);
            using var transport = new CanopenAxisTransport(node, new BindAxisSetting { FeedbackPdoNumber = 1, FeedbackMaxAgeMs = 1, CommunicationTimeoutMs = 100 });
            await transport.InitializeAsync(default);
            fake.Incoming.Enqueue(new CanMessage { Id = 0x181, Data = [0x27, 0, 0, 0, 0, 0, 0, 0], TimestampNs = 1 });
            await node.ReadObjectAsync(0x6041, 0, default);
            await Task.Delay(20);
            await Throws<TimeoutException>(() => transport.ReadFeedbackAsync(true, false, false, default));
        });
        await Test("活动操作退出后完成延后释放", async () =>
        {
            var (axis, fake) = await Axis(enable: false);
            using var session = new AxisSession(axis, new BindAxisSetting { CommunicationTimeoutMs = 10 }, default, (_, _) => { });
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = session.ExecuteAsync((_, _) => release.Task, true, 1, default);
            await session.CloseAsync(); Check(!fake.Disposed);
            release.SetResult(); await Throws<OperationCanceledException>(() => operation);
            Check(fake.Disposed);
        });
        await Test("CSP 使能前重新核对当前位置", async () =>
        {
            var (axis, fake) = await Axis(OperationMode.CSP, false);
            await axis.WriteCyclicAsync(OperationMode.CSP, fake.Position, default);
            fake.Position++;
            await Throws<InvalidOperationException>(() => axis.EnableAsync(default));
        });
        await Test("输出只允许已声明的完整变量路径", async () =>
        {
            var (context, state) = TestContext.Create(new ReadStatePlugin(), new ReadStateSetting());
            await Throws<ArgumentException>(() => Sync(() => SettingValidation.ValidateOutput(context, "Value", typeof(double))));
            await Throws<ArgumentException>(() => Sync(() => SettingValidation.ValidateOutput(context, "Locals.Missing", typeof(double))));
            state.Variables["Locals.Value"] = 0.0;
            SettingValidation.ValidateOutput(context, "Locals.Value", typeof(double));
            SettingValidation.Output(context, "Locals.Value", 42.0); Check((double)state.Variables["Locals.Value"]! == 42);
        });
        await Test("取消执行结果为 Aborted", async () =>
        {
            var plugin = new ReadStatePlugin(); var (context, _) = TestContext.Create(plugin, new ReadStateSetting());
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            Check((await plugin.CreateExecutor().ExecuteAsync(context, cancel.Token)).StepResult.Status == TestStatus.Aborted);
        });
        await Test("未绑定轴返回中文 Error", async () =>
        {
            var plugin = new ReadStatePlugin(); var (context, _) = TestContext.Create(plugin, new ReadStateSetting { TimeoutMs = 30000 });
            var result = await plugin.CreateExecutor().ExecuteAsync(context, default);
            Check(result.StepResult.Status == TestStatus.Error && result.StepResult.Error?.Message.Contains("不存在") == true);
        });
        await Test("无 EtherCAT 工厂不能伪装成功", async () =>
        {
            var plugin = new BindAxisPlugin(); var (context, _) = TestContext.Create(plugin, new BindAxisSetting { Transport = TransportKind.EtherCAT, TimeoutMs = 30000 });
            var result = await plugin.CreateExecutor().ExecuteAsync(context, default);
            Check(result.StepResult.Status == TestStatus.Error && result.StepResult.Error?.Message.Contains("EtherCAT") == true);
        });
        await Test("执行器超时结果为 Error 而非 Aborted", async () =>
        {
            var plugin = new EnablePlugin(); var (context, state) = TestContext.Create(plugin, new EnableSetting { TimeoutMs = 100, PollIntervalMs = 1 });
            var (axis, fake) = await Axis(enable: false); fake.Freeze = true;
            using var session = new AxisSession(axis, new BindAxisSetting(), default, (_, _) => { });
            state.Resources[AxisSession.Key("Axis1")] = session;
            var result = await plugin.CreateExecutor().ExecuteAsync(context, default);
            Check(result.StepResult.Status == TestStatus.Error && result.StepResult.Error?.Message.Contains("超时") == true);
        });
        await ConcurrencyAndModeTests.RunAsync(Test);
    }
}
