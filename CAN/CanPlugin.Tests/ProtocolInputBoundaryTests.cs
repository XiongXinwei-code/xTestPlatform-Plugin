using System.Reflection;
using CAN.UDS;
using CAN.UDS.Models;
using CAN.XCP;
using CAN.XCP.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

public sealed class ProtocolInputBoundaryTests
{
    [Theory]
    [InlineData("raw")]
    [InlineData("did")]
    [InlineData("routine")]
    public async Task UdsOddNibbleMustFailBeforeSending(string kind)
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        var raw = new FakeAdapter(); state.Adapter = raw;
        raw.OnWrite = _ => raw.Incoming.Add(new() { Id = 0x70C, Data = [3, 0x7F, 0x31, 0x13] });
        if (kind == "raw") { state.Plugin = new UdsRawRequestPlugin(); state.Setting = new UdsRawRequestSetting { RequestData = "\"10 0\"", WaitResponse = false }; }
        else if (kind == "did") { state.Plugin = new UdsWriteDataByIdPlugin(); state.Setting = new UdsWriteDataByIdSetting { Did = "\"0xF184\"", Data = "\"12 3\"" }; }
        else { state.Plugin = new UdsRoutineControlPlugin(); state.Setting = new UdsRoutineControlSetting { RoutineId = "\"0xFF00\"", OptionRecord = "\"12 3\"" }; }
        var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Empty(raw.Sent);
    }

    [Fact]
    public async Task RawSecurityKeyMustNotAppearInLogsOrResultDisplay()
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Adapter = new FakeAdapter(); state.Plugin = new UdsRawRequestPlugin();
        state.Setting = new UdsRawRequestSetting { RequestData = "\"27 12 DE AD BE EF\"", WaitResponse = false };
        var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        Assert.DoesNotContain("DE AD BE EF", string.Join("\n", state.Logs));
        Assert.DoesNotContain("DE AD BE EF", result.StepResult.Value?.ToString() ?? "");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelInXcpResponseLogMustNotPublishOldExecutionResult(bool connect)
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        var raw = new FakeAdapter(); state.Adapter = raw;
        if (connect) { state.Plugin = new XcpConnectPlugin(); state.Setting = new XcpConnectSetting { ResourceVariable = "Locals.Value" }; }
        else { state.Plugin = new XcpShortUploadPlugin(); state.Setting = new XcpShortUploadSetting { ReadLength = 1, Address = "\"0x1000\"", ResultVariable = "Locals.Value" }; }
        raw.OnWrite = _ => raw.Incoming.Add(new() { Data = connect ? [0xFF, 1, 0, 8, 8, 0, 1, 1] : [0xFF, 0x12] });
        using var stop = new CancellationTokenSource(); state.OnLog = _ => stop.Cancel();
        var result = await state.Plugin.CreateExecutor().ExecuteAsync(context, stop.Token);
        Assert.Equal(TestStatus.Aborted, result.StepResult.Status); Assert.Empty(state.Variables);
    }
}
