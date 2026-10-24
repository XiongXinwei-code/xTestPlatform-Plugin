using CiA402.Core;
using CiA402.Models;
using Xunit;

namespace CiA402.Tests;

public sealed class ControlSafetyTests
{
    [Theory]
    [InlineData(OperationMode.PP)]
    [InlineData(OperationMode.HM)]
    public async Task 使能不携带绑定时遗留的运动启动位(OperationMode mode)
    {
        var drive = new FakeAxisTransport { Mode = mode, Control = 0x007F };
        var axis = new AxisController(drive, new BindAxisSetting()) { PollIntervalMs = 1 };
        using var transport = axis.Transport;
        await axis.InitializeAsync(default);
        await axis.EnableAsync(default);
        Assert.All(drive.Writes.Where(w => w.Index == 0x6040), write =>
            Assert.Equal(0, ObjectCodec.Decode(write.Data, 2, false) & 0x0070));
        Assert.Equal(DriveState.OperationEnabled, AxisController.DecodeState(drive.Status));
    }

    [Theory]
    [InlineData(OperationMode.PV, 0x60FF, 4)]
    [InlineData(OperationMode.PT, 0x6071, 2)]
    public async Task 重新使能前清除上次遗留的速度或转矩目标(OperationMode mode, int index, int bytes)
    {
        var drive = new FakeAxisTransport { Mode = mode, Control = 0x1F };
        drive.Objects[((ushort)index, 0)] = ObjectCodec.Encode(-100, bytes, true);
        var axis = new AxisController(drive, new BindAxisSetting()) { PollIntervalMs = 1 };
        using var transport = axis.Transport;
        await axis.InitializeAsync(default);
        await axis.EnableAsync(default);
        Assert.Equal(0, ObjectCodec.Decode(drive.Objects[((ushort)index, 0)], bytes, true));
        var targetWrite = drive.Writes.FindIndex(w => w.Index == index);
        var enableWrite = drive.Writes.FindIndex(w => w.Index == 0x6040 && (ObjectCodec.Decode(w.Data, 2, false) & 0xF) == 0xF);
        Assert.True(targetWrite >= 0 && targetWrite < enableWrite);
    }

    [Fact]
    public async Task 已使能时Enable幂等且不覆盖当前速度目标()
    {
        var drive = new FakeAxisTransport { Mode = OperationMode.PV };
        var axis = new AxisController(drive, new BindAxisSetting()) { PollIntervalMs = 1 };
        using var transport = axis.Transport;
        await axis.InitializeAsync(default);
        await axis.EnableAsync(default);
        await axis.RunVelocityAsync(55, 100, 100, default);
        drive.Writes.Clear();
        await axis.EnableAsync(default);
        Assert.Empty(drive.Writes);
        Assert.Equal(55, ObjectCodec.Decode(drive.Objects[(0x60FF, 0)], 4, true));
    }

    [Fact]
    public async Task 模式显示已匹配但驱动故障时不返回成功()
    {
        var probe = new ProbeAxisTransport();
        var axis = new AxisController(probe, new BindAxisSetting());
        using var transport = axis.Transport;
        await axis.InitializeAsync(default);
        probe.WriteHandler = async (index, data, ct) =>
        {
            await probe.Drive.WriteObjectAsync(index, 0, data, ct);
            if (index == 0x6060) probe.Drive.Status = 0x8;
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => axis.SetModeAsync(OperationMode.PV, default));
    }

    [Theory]
    [InlineData(0x40)]
    [InlineData(0x21)]
    [InlineData(0x23)]
    public async Task 未使能状态切换模式不得因旧控制字而意外使能(int status)
    {
        var drive = new FakeAxisTransport { Mode = OperationMode.PV, Status = (ushort)status, Control = 0x007F };
        drive.Objects[(0x60FF, 0)] = ObjectCodec.Encode(-100, 4, true);
        var axis = new AxisController(drive, new BindAxisSetting());
        using var transport = axis.Transport;
        await axis.InitializeAsync(default);
        await axis.SetModeAsync(OperationMode.PT, default);
        Assert.All(drive.Writes.Where(w => w.Index == 0x6040), w => Assert.Equal(0, ObjectCodec.Decode(w.Data, 2, false) & 0x8));
        Assert.NotEqual(DriveState.OperationEnabled, AxisController.DecodeState(drive.Status));
        Assert.Equal(OperationMode.PT, drive.Mode);
    }
}
