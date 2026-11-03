using System.Reflection;
using CAN.UDS;
using CAN.UDS.Models;
using CAN.XCP;
using CAN.XCP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CAN.Tests;

public sealed class CanSuccessStatusTests
{
    [Fact]
    public async Task Suppressed_session_response_passes_after_submission_without_reading()
    {
        var raw = new FakeAdapter();
        var plugin = new UdsDiagSessionPlugin();
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>();
        var state = (CloseContext)(object)context;
        state.Adapter = raw; state.Plugin = plugin;
        state.Setting = new UdsDiagSessionSetting { SuppressPositiveResponse = true };
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Passed, result.StepResult.Status);
        Assert.Single(raw.Sent);
        Assert.False(raw.ReadEntered.Task.IsCompleted);
        Assert.Equal(0x83, raw.Sent.Single().Data[2]);
    }

    [Fact]
    public async Task Successful_seed_does_not_turn_rejected_key_into_pass()
    {
        var raw = new FakeAdapter();
        raw.OnWrite = frame => raw.Incoming.Add(new() { Id = 0x7E8,
            Data = frame.Data[2] == 1 ? [3, 0x67, 1, 0x12] : [3, 0x7F, 0x27, 0x35] });
        var plugin = new UdsSecurityAccessPlugin();
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>();
        var state = (CloseContext)(object)context;
        state.Adapter = raw; state.Plugin = plugin;
        state.Setting = new UdsSecurityAccessSetting { KeyExpression = "new byte[] { 0x12 }" };
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Equal(2, raw.Sent.Count);
        Assert.Contains("提交密钥", result.StepResult.Error!.Message);
    }

    public static IEnumerable<object[]> ProtocolCases() =>
        from name in new[] { "Session", "ReadDid", "WriteDid", "Routine", "ClearDtc", "ReadDtc", "Security", "Raw",
            "XcpConnect", "XcpDisconnect", "XcpUpload", "XcpDownload" }
        from rejected in new[] { false, true }
        select new object[] { name, rejected };

    [Theory]
    [MemberData(nameof(ProtocolCases))]
    public async Task Published_protocol_steps_pass_only_after_a_successful_response(string name, bool rejected)
    {
        (IStepPlugin Plugin, object Setting) entry = name switch
        {
            "Session" => (new UdsDiagSessionPlugin(), new UdsDiagSessionSetting()),
            "ReadDid" => (new UdsReadDataByIdPlugin(), new UdsReadDataByIdSetting()),
            "WriteDid" => (new UdsWriteDataByIdPlugin(), new UdsWriteDataByIdSetting { Data = "\"12\"" }),
            "Routine" => (new UdsRoutineControlPlugin(), new UdsRoutineControlSetting()),
            "ClearDtc" => (new UdsClearDtcPlugin(), new UdsClearDtcSetting()),
            "ReadDtc" => (new UdsReadDtcPlugin(), new UdsReadDtcSetting()),
            "Security" => (new UdsSecurityAccessPlugin(), new UdsSecurityAccessSetting { KeyExpression = "new byte[] { 0x12 }" }),
            "Raw" => (new UdsRawRequestPlugin(), new UdsRawRequestSetting { RequestData = "\"22 F1 90\"" }),
            "XcpConnect" => (new XcpConnectPlugin(), new XcpConnectSetting()),
            "XcpDisconnect" => (new XcpDisconnectPlugin(), new XcpDisconnectSetting()),
            "XcpUpload" => (new XcpShortUploadPlugin(), new XcpShortUploadSetting()),
            "XcpDownload" => (new XcpShortDownloadPlugin(), new XcpShortDownloadSetting()),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        var (plugin, setting) = entry;
        var raw = new FakeAdapter();
        raw.OnWrite = frame =>
        {
            if (setting is XcpCommonSetting)
            {
                raw.Incoming.Add(new() { Id = 0x7E9, Data = rejected ? [0xFE, 0x20] : [0xFF, 0x11, 0, 8, 8, 0, 1, 1] });
                return;
            }
            // All requests in this fixture are ISO-TP single frames.
            Assert.InRange(frame.Data[0], (byte)1, (byte)7);
            var request = frame.Data.AsSpan(1, frame.Data[0]).ToArray();
            byte[] response = rejected ? [0x7F, request[0], 0x22] : request[0] switch
            {
                0x10 => [0x50, request[1], 0, 50, 0, 200],
                0x22 => [0x62, request[1], request[2], 0x42],
                0x2E => [0x6E, request[1], request[2]],
                0x31 => [0x71, request[1], request[2], request[3], 0],
                0x14 => [0x54],
                0x19 => [0x59, request[1], 0xFF],
                0x27 => [0x67, request[1], 0x12],
                _ => throw new InvalidOperationException("Unexpected fixture SID")
            };
            raw.Incoming.Add(new() { Id = 0x7E8, Data = [(byte)response.Length, .. response] });
        };
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>();
        var state = (CloseContext)(object)context;
        state.Adapter = raw; state.Plugin = plugin; state.Setting = setting;
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.True(result.StepResult.Status == (rejected ? TestStatus.Error : TestStatus.Passed), result.StepResult.Error?.ToString());
        Assert.NotEmpty(raw.Sent);
        if (rejected) Assert.False(string.IsNullOrWhiteSpace(result.StepResult.Error?.Message));
        else Assert.Null(result.StepResult.Error);
    }
}
