using System.Diagnostics;
using System.Reflection;
using CAN.CANopen;
using CAN.Flash.Executors;
using CAN.Models;
using CAN.UDS;
using CAN.UDS.IsoTp;
using CAN.Flash;
using CAN.Flash.Models;
using CAN.XCP;
using CAN.XCP.Models;
using CAN.XCP.Executors;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

public sealed class AuditProtocolTests
{
    [Theory]
    [InlineData("22F189", "62F18801")]
    [InlineData("22F189", "62")]
    [InlineData("2EF18400", "6EF185")]
    [InlineData("3101FF00", "7101FF01")]
    [InlineData("3601AA", "7602")]
    [InlineData("3601AA", "76")]
    [InlineData("340044", "7420")]
    public async Task UdsRejectsWrongEchoOrTruncatedPositive(string request, string response)
    {
        var fake = new FakeAdapter();
        var bytes = Convert.FromHexString(response);
        fake.Incoming.Add(new() { Id = 0x70C, Data = [(byte)bytes.Length, .. bytes] });
        var result = await new UdsClient(fake, 0x704, 0x70C).RequestAsync(Convert.FromHexString(request));
        Assert.False(result.IsPositive);
        Assert.NotNull(result.ProtocolError);
    }

    [Fact]
    public async Task UdsRequestsShareOneAdapterTransaction()
    {
        var fake = new FakeAdapter();
        var a = Task.Run(() => new UdsClient(fake, 0x704, 0x70C).RequestAsync([0x22, 0xF1, 0x89]));
        await fake.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var b = Task.Run(() => new UdsClient(fake, 0x704, 0x70C).RequestAsync([0x22, 0xF1, 0x88]));
        await Task.Delay(50);
        var writesBeforeResponse = fake.Sent.Count;
        fake.Incoming.Add(new() { Id = 0x70C, Data = [4, 0x62, 0xF1, 0x89, 1] });
        fake.Incoming.Add(new() { Id = 0x70C, Data = [4, 0x62, 0xF1, 0x88, 2] });
        await Task.WhenAll(a, b);
        Assert.Equal(1, writesBeforeResponse);
        Assert.True(a.Result.IsPositive);
        Assert.True(b.Result.IsPositive);
    }

    [Fact]
    public async Task LaterFlowControlOverflowStopsBeforeNextBlock()
    {
        var fake = new FakeAdapter();
        fake.Incoming.Add(new() { Data = [0x30, 1, 0] });
        fake.Incoming.Add(new() { Data = [0x32, 0, 0] });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new IsoTpTransport(fake, 1, 2).SendAsync(new byte[30]));
        Assert.Equal(2, fake.Sent.Count);
    }

    [Fact]
    public async Task FlowControlWaitThenCtsCanProceed()
    {
        var fake = new FakeAdapter();
        fake.Incoming.Add(new() { Data = [0x31, 0, 0] });
        fake.Incoming.Add(new() { Data = [0x30, 0, 0] });
        await new IsoTpTransport(fake, 1, 2).SendAsync(new byte[20]);
        Assert.Equal(3, fake.Sent.Count);
    }

    [Fact]
    public async Task LaterWaitAndNewBlockSizeAreApplied()
    {
        var fake = new FakeAdapter();
        fake.Incoming.Add(new() { Data = [0x30, 1, 0] });
        fake.Incoming.Add(new() { Data = [0x31, 0, 0] });
        fake.Incoming.Add(new() { Data = [0x30, 0, 0] });
        await new IsoTpTransport(fake, 1, 2).SendAsync(new byte[30]);
        Assert.Equal(5, fake.Sent.Count);
    }

    [Fact]
    public async Task ResponsePendingKeepsRequestUntilMatchingFinalResponse()
    {
        var fake = new FakeAdapter();
        fake.Incoming.Add(new() { Data = [3, 0x7F, 0x22, 0x78] });
        fake.Incoming.Add(new() { Data = [4, 0x62, 0xF1, 0x89, 7] });
        var result = await new UdsClient(fake, 1, 2).RequestAsync([0x22, 0xF1, 0x89]);
        Assert.True(result.IsPositive);
        Assert.Equal(7, result.RawBytes[^1]);
        Assert.Single(fake.Sent);
    }

    [Fact]
    public async Task CanopenPreservesEarlierIngressAge()
    {
        var fake = new FakeAdapter();
        var old = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 5;
        fake.Incoming.Add(new() { Id = 0x181, Data = [1], IngressMonotonicTicks = old });
        var frame = await CanopenConnectionAccess.For(fake).ReceiveAsync(0x181, 1000, default);
        Assert.Equal(old, frame!.ObservedTimestamp);
        Assert.True(frame.QueueAge >= TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void FlashEncodingDoesNotTruncateAddress()
    {
        var method = typeof(CanFlashExecutor).GetMethod("EncodeValue", BindingFlags.Static | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [0x100u, 1]));
        Assert.IsType<ArgumentOutOfRangeException>(error.InnerException);
    }

    [Fact]
    public async Task OversizedFirstFrameRejectedBeforeAllocatingOrRequestingData()
    {
        var fake = new FakeAdapter();
        fake.Incoming.Add(new() { Data = [0x10, 0, 0, 0x40, 0, 1, 0, 0] });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new IsoTpTransport(fake, 1, 2).ReceiveAsync(100));
        Assert.Equal(0x32, Assert.Single(fake.Sent).Data[0]);
    }

    [Fact]
    public async Task UdsTimeoutQuarantinesLateRepliesForNextClient()
    {
        var fake = new FakeAdapter();
        Assert.True((await new UdsClient(fake, 1, 2, 20).RequestAsync([0x22, 0xF1, 0x89])).IsTimeout);
        fake.Incoming.Add(new() { Data = [4, 0x62, 0xF1, 0x89, 9] });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new UdsClient(fake, 1, 2).RequestAsync([0x22, 0xF1, 0x89]));
        Assert.Single(fake.Sent);
    }

    [Fact]
    public async Task UnknownArrivalTimeIsNotReportedFresh()
    {
        var fake = new FakeAdapter(); fake.Incoming.Add(new() { Id = 0x181, Data = [1] });
        var frame = await CanopenConnectionAccess.For(fake).ReceiveAsync(0x181, 200, default);
        Assert.False(frame!.HasKnownObservationTime);
        Assert.Equal(TimeSpan.MaxValue, frame.QueueAge);
    }

    [Fact]
    public async Task FlashLaterUnencodableSegmentPreventsEveryEraseAndDownload()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            await System.IO.File.WriteAllTextAsync(path, ":020000040000FA\n:0100000001FE\n:020000040001F9\n:0100000002FD\n:00000001FF\n");
            var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
            var fake = new FakeAdapter(); state.Adapter = fake; state.Plugin = new CanFlashPlugin();
            state.Setting = new CanFlashSetting { ConnectionName = "\"CAN1\"", FilePath = "@\"" + path + "\"", Format = FirmwareFormat.IntelHex, AddressAndLengthFormatId = "\"0x11\"", PreDownloadDelayMs = 0, EraseBeforeDownload = true, CheckMode = FlashCheckMode.None };
            var result = await new CanFlashExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("无法精确编码", result.StepResult.Error!.Message);
            Assert.Empty(fake.Sent);
        }
        finally { System.IO.File.Delete(path); }
    }

    [Fact]
    public async Task XcpConnectWritesSequenceVariableNotDisposableResource()
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        var fake = new FakeAdapter(); fake.Incoming.Add(new() { Data = [0xFF, 0x11, 0, 8, 8, 0, 1, 1] });
        state.Adapter = fake; state.Plugin = new XcpConnectPlugin();
        state.Setting = new XcpConnectSetting { ConnectionName = "\"CAN1\"", ResourceVariable = "Locals.Mask" };
        var result = await new XcpConnectExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        Assert.Equal((byte)0x11, state.Variables["Locals.Mask"]);
        Assert.Equal(0, state.ResourceWrites);
    }
}
