using System.Reflection;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

public sealed class RawFrameBoundaryTests
{
    [Fact]
    public async Task ClassicConnectionCannotSilentlyDowngradeRequestedFdFrame()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        owner.Open(new() { Protocol = CanProtocolType.Classic });
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => owner.WriteAsync(new() { Id = 1, IsFd = true, Data = new byte[12] }, 1000));
            Assert.Empty(raw.Sent);
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData("12 3", "0x700", false, false)]
    [InlineData("00 01 02 03 04 05 06 07 08", "0x700", false, false)]
    [InlineData("00", "0x800", false, false)]
    [InlineData("00", "0x20000000", true, false)]
    [InlineData("oversize", "0x700", false, true)]
    public async Task ActualWriteRejectsInvalidFrameBeforeDriver(string data, string id, bool extended, bool fd)
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); state.Adapter = owner;
        state.Plugin = new CanWritePlugin();
        state.Setting = new CanWriteSetting { Data = $"\"{(data == "oversize" ? string.Join(" ", Enumerable.Repeat("00", 65)) : data)}\"",
            CanId = $"\"{id}\"", FrameType = extended ? CanFrameType.Extended : CanFrameType.Standard, UseFdFrame = fd };
        try { var result = await state.Plugin.CreateExecutor().ExecuteAsync(context); Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Empty(raw.Sent); }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData("12 3", "0x700")]
    [InlineData("00 01 02 03 04 05 06 07 08", "0x700")]
    [InlineData("00", "0x800")]
    public async Task ActualCyclicWorkerRejectsInvalidFrameBeforeDriver(string data, string id)
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); state.Adapter = owner;
        var submitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); raw.OnWrite = _ => submitted.TrySetResult();
        state.Plugin = new CanCyclicSendStartPlugin();
        state.Setting = new CanCyclicSendStartSetting { Messages = [new() { CanId = $"\"{id}\"", Data = $"\"{data}\"", CycleTimeMs = 1 }] };
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context); Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            var jobs = (Dictionary<string, CanBackgroundJob>)typeof(CanConnectionResource).GetField("_jobs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
            var job = Assert.Single(jobs).Value;
            await Task.WhenAny(job.Completion, submitted.Task).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Empty(raw.Sent); await job.Completion; Assert.NotNull(job.Error);
        }
        finally { await Record.ExceptionAsync(() => owner.CloseAsync()); Assert.True(owner.IsClosed); }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(12)] [InlineData(16)] [InlineData(20)] [InlineData(24)] [InlineData(32)] [InlineData(48)] [InlineData(64)]
    public async Task LegalFdPayloadLengthsArePreserved(int length)
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try { await owner.WriteAsync(new() { Id = 0x1FFFFFFF, FrameType = CanFrameType.Extended, IsFd = true, Data = Enumerable.Range(0, length).Select(i => (byte)i).ToArray() }, 1000); Assert.Equal(length, Assert.Single(raw.Sent).Data.Length); }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData("12-34,56 78")]
    [InlineData("12345678")]
    public void LegacyHexSeparatorsRemainAccepted(string text) => Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x78 }, CanHelper.ParseHexData(text));
}
