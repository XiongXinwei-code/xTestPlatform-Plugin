using CAN.CANopen;
using CAN.CANopen.Models;
using CAN.Helpers;
using CAN.Runtime;
using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Models;
using Xunit;

namespace CiA402.Tests;

public sealed class MixedCanPluginTests
{
    [Fact]
    public async Task Bound_axis_can_use_generic_Canopen_object_steps_and_unbind_does_not_close_shared_Can()
    {
        var raw = new FakeCanAdapter(); raw.Object(0x6040, 0, 0, 2); raw.Object(0x6041, 0, 0x40, 2); raw.Object(0x2000, 0, 7, 4);
        var owner = new CanConnectionResource(raw);
        var bind = new BindAxisPlugin(); var (context, state) = TestContext.Create(bind, new BindAxisSetting());
        state.Resources[CanHelper.GetAdapterKey("CAN1")] = owner;
        AxisSession? axis = null;
        try
        {
            var bound = await bind.CreateExecutor().ExecuteAsync(context);
            Assert.True(bound.StepResult.Status == TestStatus.Completed, bound.StepResult.Error?.Detail);
            axis = Assert.IsType<AxisSession>(state.Resources[AxisSession.Key("Axis1")]);
            var read = new CanopenSdoReadPlugin();
            var (readContext, readState) = TestContext.Create(read, new CanopenSdoReadSetting { Index = "0x2000", DataType = CanopenDataType.Unsigned32 });
            readState.Resources = state.Resources;
            Assert.Equal(TestStatus.Completed, (await read.CreateExecutor().ExecuteAsync(readContext)).StepResult.Status);
            var write = new CanopenSdoWritePlugin();
            var (writeContext, writeState) = TestContext.Create(write, new CanopenSdoWriteSetting { Index = "0x2000", Value = "9", DataType = CanopenDataType.Unsigned32 });
            writeState.Resources = state.Resources;
            Assert.Equal(TestStatus.Completed, (await write.CreateExecutor().ExecuteAsync(writeContext)).StepResult.Status);
            Assert.Equal(new byte[] { 9, 0, 0, 0 }, raw.Objects[(0x2000, 0)]);
            await owner.WriteAsync(new() { Id = 0x202, Data = [1] }, 2000);
            await axis.CloseAsync();
            Assert.True(owner.IsConnected); Assert.Equal(0, raw.CloseCount);
            await owner.WriteAsync(new() { Id = 0x80, Data = [0] }, 2000);
        }
        finally { if (axis != null) await axis.CloseAsync(); await owner.CloseAsync(); }
        Assert.True(owner.IsClosed); Assert.False(raw.IsConnected);
        Assert.Equal(2, raw.CloseCount); // This fake counts Close and Dispose; both belong to the parent owner.
    }
}
