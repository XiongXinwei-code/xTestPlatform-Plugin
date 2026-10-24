using CAN.CANopen;
using CAN.Models;
using CiA402.Models;
using CiA402.Transport;
using CiA402.Core;
using MessagePack;
using System.Diagnostics;
using Xunit;

namespace CiA402.Tests;

public sealed class FeedbackPdoTests
{
    [Fact]
    public async Task HostIngressWithoutCaptureEvidenceCannotRefreshFeedbackAge()
    {
        using var adapter = new FakeCanAdapter();
        var setting = Multi(); setting.CommunicationTimeoutMs = 80;
        using var transport = await Open(adapter, setting);
        var frame = Position(123);
        frame.IngressMonotonicTicks = Stopwatch.GetTimestamp();
        // Simulate an old native-queue frame arriving now: intentionally bypass producer timestamping.
        ((System.Collections.Concurrent.ConcurrentQueue<CanMessage>)adapter.Incoming).Enqueue(frame);
        await Assert.ThrowsAsync<TimeoutException>(() => transport.ReadFeedbackAsync(true, false, false, default));
    }

    private static BindAxisSetting Multi() => new()
    {
        UseMultipleFeedbackPdos = true,
        CommunicationTimeoutMs = 500,
        FeedbackMaxAgeMs = 100,
        FeedbackPdos = [new() { Number = 1, Layout = "0x6041:0:16,0x6064:0:32" }, new() { Number = 2, Layout = "0x606C:0:32,0x6077:0:16" }]
    };

    private static async Task<CanopenAxisTransport> Open(FakeCanAdapter adapter, BindAxisSetting setting)
    {
        foreach (var row in PdoConfiguration.FeedbackMappings(setting))
            adapter.Pdo(row.Number, true, (uint)(0x81 + row.Number * 0x100), row.Layout);
        var transport = new CanopenAxisTransport(CanopenConnectionAccess.For(adapter).ReserveNode(1, setting.CommunicationTimeoutMs), setting);
        try { await transport.InitializeAsync(default); return transport; }
        catch { transport.Dispose(); throw; }
    }

    private static CanMessage Position(int value, long timestamp = 0, ushort status = 0x27)
        => new() { Id = 0x181, TimestampNs = timestamp, Data = [.. ObjectCodec.Encode(status, 2, false), .. ObjectCodec.Encode(value, 4, true)] };
    private static CanMessage Velocity(int value, short torque = 0, long timestamp = 0)
        => new() { Id = 0x281, TimestampNs = timestamp, Data = [.. ObjectCodec.Encode(value, 4, true), .. ObjectCodec.Encode(torque, 2, true)] };

    [Fact]
    public async Task 反馈映射允许附带已核对的其他对象()
    {
        const string layout = "0x6041:0:16,0x6061:0:8,0x6064:0:32";
        using var adapter = new FakeCanAdapter();
        adapter.Pdo(1, true, 0x181, layout);
        using var transport = new CanopenAxisTransport(CanopenConnectionAccess.For(adapter).ReserveNode(1, 1000),
            new BindAxisSetting { FeedbackPdoNumber = 1, FeedbackPdoLayout = layout });
        await transport.InitializeAsync(default);
        adapter.Incoming.Enqueue(new CanMessage { Id = 0x181, Data = [0x27, 0, 1, 0xFF, 0xFF, 0xFF, 0xFF] });
        var feedback = await transport.ReadFeedbackAsync(true, false, false, default);
        Assert.Equal(-1, feedback.Position);
        Assert.Equal((ushort)0x27, feedback.StatusWord);
    }

    [Fact]
    public async Task 两个TPDO合并完整反馈且乱序到达不丢帧()
    {
        using var adapter = new FakeCanAdapter();
        using var transport = await Open(adapter, Multi());
        adapter.Incoming.Enqueue(Velocity(-50, -2));
        adapter.Incoming.Enqueue(Position(-123));
        var feedback = await transport.ReadFeedbackAsync(true, true, true, default);
        Assert.Equal(new AxisFeedback(0x27, -123, -50, -2), feedback);
        Assert.Equal(0, adapter.CloseCount);
    }

    [Fact]
    public async Task 每个TPDO独立检查时间戳而不是共用最后一帧时间()
    {
        using var adapter = new FakeCanAdapter();
        using var transport = await Open(adapter, Multi());
        adapter.Incoming.Enqueue(Position(1, 10_000_000));
        adapter.Incoming.Enqueue(Velocity(2, timestamp: 2_000_000));
        await transport.ReadFeedbackAsync(true, true, false, default);
        adapter.Incoming.Enqueue(Position(3, 11_000_000));
        adapter.Incoming.Enqueue(Velocity(4, timestamp: 3_000_000));
        Assert.Equal(4, (await transport.ReadFeedbackAsync(true, true, false, default)).Velocity);
    }

    [Fact]
    public async Task 只读取本次需要的TPDO且不消费未请求的反馈()
    {
        using var adapter = new FakeCanAdapter();
        using var transport = await Open(adapter, Multi());
        adapter.Incoming.Enqueue(Position(11));
        var feedback = await transport.ReadFeedbackAsync(true, false, false, default);
        Assert.Equal(11, feedback.Position);
        Assert.Null(feedback.Velocity);
        Assert.Null(feedback.Torque);
    }

    [Fact]
    public async Task 重复时间戳被忽略而下一帧仍可使用()
    {
        using var adapter = new FakeCanAdapter();
        using var transport = await Open(adapter, Multi());
        adapter.Incoming.Enqueue(Position(1, 100));
        await transport.ReadFeedbackAsync(true, false, false, default);
        adapter.Incoming.Enqueue(Position(99, 100));
        adapter.Incoming.Enqueue(Position(2, 101));
        Assert.Equal(2, (await transport.ReadFeedbackAsync(true, false, false, default)).Position);
    }

    [Fact]
    public async Task 合并窗口不满足时重新获取较早的TPDO()
    {
        using var adapter = new FakeCanAdapter();
        using var transport = await Open(adapter, Multi());
        adapter.Incoming.Enqueue(Position(1, 1));
        adapter.Incoming.Enqueue(Velocity(50, timestamp: 300_000_000));
        adapter.Incoming.Enqueue(Position(2, 305_000_000));
        Assert.Equal(2, (await transport.ReadFeedbackAsync(true, true, false, default)).Position);
    }

    [Fact]
    public async Task 本次缺帧时不能用上次调用的反馈补齐()
    {
        using var adapter = new FakeCanAdapter();
        var setting = Multi(); setting.CommunicationTimeoutMs = 100;
        using var transport = await Open(adapter, setting);
        adapter.Incoming.Enqueue(Position(1)); adapter.Incoming.Enqueue(Velocity(2));
        await transport.ReadFeedbackAsync(true, true, false, default);
        adapter.Incoming.Enqueue(Position(3));
        var error = await Assert.ThrowsAsync<TimeoutException>(() => transport.ReadFeedbackAsync(true, true, false, default));
        Assert.Contains("0x281", error.Message);
    }

    [Fact]
    public async Task 多帧读取共用一个通信期限并响应取消()
    {
        using var adapter = new FakeCanAdapter();
        var setting = Multi(); setting.CommunicationTimeoutMs = 100;
        using var transport = await Open(adapter, setting);
        adapter.Incoming.Enqueue(Position(3));
        var clock = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(() => transport.ReadFeedbackAsync(true, true, true, default));
        Assert.True(clock.ElapsedMilliseconds < 1000);
        using var cancel = new CancellationTokenSource(20);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.ReadFeedbackAsync(true, true, true, cancel.Token));
    }

    [Fact]
    public async Task TPDO长度不匹配报告对应COBID()
    {
        using var adapter = new FakeCanAdapter();
        using var transport = await Open(adapter, Multi());
        adapter.Incoming.Enqueue(new CanMessage { Id = 0x181, Data = [0x27, 0] });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ReadFeedbackAsync(true, false, false, default));
        Assert.Contains("0x181", error.Message);
    }

    [Fact]
    public async Task 跨TPDO重复字段按配置顺序选择来源()
    {
        using var adapter = new FakeCanAdapter();
        var setting = Multi(); setting.FeedbackPdos[1].Layout = "0x6041:0:16,0x606C:0:32";
        using var transport = await Open(adapter, setting);
        adapter.Incoming.Enqueue(Position(1, status: 0x27));
        adapter.Incoming.Enqueue(new CanMessage { Id = 0x281, Data = [0x21, 0, 7, 0, 0, 0] });
        var feedback = await transport.ReadFeedbackAsync(true, true, false, default);
        Assert.Equal((ushort)0x27, feedback.StatusWord);
        Assert.Equal(7, feedback.Velocity);
    }

    [Fact]
    public async Task 两个反馈TPDO不能使用同一COBID()
    {
        using var adapter = new FakeCanAdapter();
        var setting = Multi();
        adapter.Pdo(1, true, 0x181, setting.FeedbackPdos[0].Layout);
        adapter.Pdo(2, true, 0x181, setting.FeedbackPdos[1].Layout);
        using var transport = new CanopenAxisTransport(CanopenConnectionAccess.For(adapter).ReserveNode(1, 500), setting);
        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.InitializeAsync(default));
    }

    [Fact]
    public async Task 目标RPDO不能与反馈TPDO使用同一COBID()
    {
        using var adapter = new FakeCanAdapter();
        var setting = new BindAxisSetting { CspPdoNumber = 1, FeedbackPdoNumber = 1 };
        adapter.Pdo(1, false, 0x181, setting.CspPdoLayout);
        adapter.Pdo(1, true, 0x181, setting.FeedbackPdoLayout);
        using var transport = new CanopenAxisTransport(CanopenConnectionAccess.For(adapter).ReserveNode(1, 500), setting);
        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.InitializeAsync(default));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("null-item")]
    [InlineData("duplicate")]
    [InlineData("range")]
    [InlineData("status")]
    [InlineData("width")]
    [InlineData("length")]
    public void 错误反馈配置在通信前被拒绝(string issue)
    {
        var setting = Multi();
        switch (issue)
        {
            case "empty": setting.FeedbackPdos.Clear(); break;
            case "null": setting.FeedbackPdos = null!; break;
            case "null-item": setting.FeedbackPdos[0] = null!; break;
            case "duplicate": setting.FeedbackPdos[1].Number = 1; break;
            case "range": setting.FeedbackPdos[0].Number = 513; break;
            case "status": setting.FeedbackPdos[0].Layout = "0x6064:0:32"; break;
            case "width": setting.FeedbackPdos[0].Layout = "0x6041:0:8,0x6064:0:32"; break;
            case "length": setting.FeedbackPdos[0].Layout += ",0x606C:0:32"; break;
        }
        Assert.NotEmpty(SettingValidation.Validate(setting));
    }

    [Fact]
    public void RPDO目标语义也在静态校验中拒绝()
    {
        Assert.NotEmpty(SettingValidation.Validate(new BindAxisSetting { CspPdoNumber = 1, CspPdoLayout = "0x60FF:0:32" }));
        Assert.Empty(SettingValidation.Validate(Multi()));
        Assert.NotEmpty(SettingValidation.Validate(new ReadDiagnosticsSetting()));
    }

    [Fact]
    public void 旧版本默认保留单TPDO配置且新版本集合可往返保存()
    {
        var serializer = new BindAxisPlugin().CreateSerializer();
        var legacy = MessagePackSerializer.Serialize(new Dictionary<string, object>
        {
            ["AxisName"] = "\"Axis1\"", ["FeedbackPdoNumber"] = 2,
            ["FeedbackPdoLayout"] = "0x6041:0:16,0x6064:0:32"
        }, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
        var restored = (BindAxisSetting)serializer.Deserialize(legacy, 1);
        Assert.False(restored.UseMultipleFeedbackPdos);
        Assert.Equal(2, restored.FeedbackPdoNumber);
        Assert.Empty(restored.FeedbackPdos);
        Assert.Equal(2, serializer.SettingVersion);
        var copy = (BindAxisSetting)serializer.Deserialize(serializer.Serialize(Multi()), serializer.SettingVersion);
        Assert.True(copy.UseMultipleFeedbackPdos);
        Assert.Equal(new[] { 1, 2 }, copy.FeedbackPdos.Select(p => p.Number));
    }
}
