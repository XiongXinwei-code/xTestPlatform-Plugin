using System.Reflection;
using System.Text.Json;
using CAN.Dbc;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcReadSelectionTests
{
    private static CanReceiveRouter Router(CanConnectionResource owner) => (CanReceiveRouter)typeof(CanConnectionResource)
        .GetField("_router", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
    private static async Task Ready(CanReceiveRouter router)
    {
        using var deadline = new CancellationTokenSource(5000);
        while (router.CaptureCount == 0) await Task.Delay(5, deadline.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selection_is_per_ID_filters_JSON_and_does_not_write_deselected_mapping(bool capture)
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        DbcReadSetting setting = capture ? new DbcCaptureSetting { MaxFrames = 2, DurationMs = 5000 } : new DbcReadSetting();
        setting.Document = new() { Text = DbcCycleTimingTests.Text }; setting.UseMessageList = true; setting.ResultVariable = "Locals.Json";
        setting.Messages =
        [
            new() { MessageId = 291, SignalOutputs = [new() { Name = "Value", Selected = false, TargetVariable = "Locals.Missing" },
                new() { Name = "Counter", TargetVariable = "Locals.Output" }] },
            new() { MessageId = 292, SignalOutputs = [new() { Name = "Counter", Selected = false }] }
        ];
        var context = DispatchProxy.Create<IExecutionContext, DbcOutputTestContext>();
        var state = (DbcOutputTestContext)(object)context;
        state.Plugin = capture ? new DbcCapturePlugin() : new DbcReadPlugin(); state.Setting = setting; state.Adapter = owner;
        state.Variables["Locals.Json"] = "old"; state.Variables["Locals.Output"] = capture ? new List<int> { -1 } : -1;
        try
        {
            var task = state.Plugin.CreateExecutor().ExecuteAsync(context); await Ready(router);
            router.Publish(new() { Id = 291, Data = [12, 34] }); router.Publish(new() { Id = 292, Data = [56, 78] });
            var result = await task;
            Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());
            using var json = JsonDocument.Parse((string)state.Variables["Locals.Json"]!);
            var frames = json.RootElement.GetProperty("frames").EnumerateArray().ToArray();
            Assert.Equal(new[] { "Counter" }, frames[0].GetProperty("signals").EnumerateArray().Select(signal => signal.GetProperty("name").GetString()));
            Assert.Equal(new[] { "Value" }, frames[1].GetProperty("signals").EnumerateArray().Select(signal => signal.GetProperty("name").GetString()));
            Assert.Equal("0C22", frames[0].GetProperty("data").GetString()); // raw payload is never trimmed
            if (capture) Assert.Equal(new[] { 34 }, Assert.IsType<List<int>>(state.Variables["Locals.Output"]));
            else Assert.Equal(34, state.Variables["Locals.Output"]);
            Assert.DoesNotContain("Locals.Missing", state.Variables.Keys);
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("all")]
    public async Task Invalid_or_ignored_selection_is_rejected_before_subscribing(string kind)
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        DbcReadSetting setting = kind == "all" ? new DbcCaptureSetting { AllDbcMessages = true } : new DbcReadSetting();
        setting.Document = new() { Text = DbcCycleTimingTests.Text }; setting.UseMessageList = false; setting.MessageId = 291; setting.ResultVariable = "Locals.Json";
        setting.SignalOutputs = [new() { Name = kind == "unknown" ? "Missing" : "Value", Selected = false }];
        if (kind == "duplicate") setting.SignalOutputs.Add(new() { Name = "Value" });
        var context = DispatchProxy.Create<IExecutionContext, DbcOutputTestContext>(); var state = (DbcOutputTestContext)(object)context;
        state.Plugin = kind == "all" ? new DbcCapturePlugin() : new DbcReadPlugin(); state.Setting = setting; state.Adapter = owner;
        state.Variables["Locals.Json"] = "old";
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(0, router.CaptureCount);
            Assert.Equal("old", state.Variables["Locals.Json"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Deselecting_checksum_and_covered_signal_keeps_full_payload_CRC_diagnostics(bool capture, bool corrupt)
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        DbcReadSetting setting = capture ? new DbcCaptureSetting { MaxFrames = 1, DurationMs = 5000 } : new DbcReadSetting();
        setting.Document = new() { Text = """
            BO_ 291 Protected: 3 ECU
             SG_ Covered : 0|8@1+ (1,0) [0|255] "" ECU
             SG_ Visible : 8|8@1+ (1,0) [0|255] "" ECU
             SG_ Checksum : 16|8@1+ (1,0) [0|255] "" ECU
            """ };
        setting.UseMessageList = false; setting.MessageId = 291; setting.ResultVariable = "Locals.Json";
        setting.Protection = new() { CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Crc8,
            CrcStartByte = 2, CrcEndByte = 2, CrcDataStartByte = 0, CrcDataEndByte = 1 };
        var payload = new CanPayloadProcessor(setting.Protection).Prepare([12, 34, 0]);
        if (corrupt) payload[2] ^= 0x01;
        var context = DispatchProxy.Create<IExecutionContext, DbcOutputTestContext>(); var state = (DbcOutputTestContext)(object)context;
        state.Plugin = capture ? new DbcCapturePlugin() : new DbcReadPlugin(); state.Setting = setting; state.Adapter = owner;
        state.Variables["Locals.Json"] = "old";
        try
        {
            async Task<JsonElement> Receive()
            {
                var task = state.Plugin.CreateExecutor().ExecuteAsync(context); await Ready(router);
                router.Publish(new() { Id = 291, Data = payload });
                var result = await task;
                Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());
                using var json = JsonDocument.Parse((string)state.Variables["Locals.Json"]!);
                return (capture ? json.RootElement.GetProperty("frames")[0] : json.RootElement.GetProperty("frame")).Clone();
            }
            var all = await Receive();
            setting.SignalOutputs = [new() { Name = "Covered", Selected = false }, new() { Name = "Checksum", Selected = false }];
            var filtered = await Receive();
            Assert.Equal(3, all.GetProperty("signals").GetArrayLength());
            Assert.Equal("Visible", Assert.Single(filtered.GetProperty("signals").EnumerateArray()).GetProperty("name").GetString());
            Assert.Equal(all.GetProperty("data").GetString(), filtered.GetProperty("data").GetString());
            Assert.Equal(all.GetProperty("integrity").GetRawText(), filtered.GetProperty("integrity").GetRawText());
            Assert.Equal(!corrupt, filtered.GetProperty("integrity").GetProperty("crcValid").GetBoolean());
        }
        finally { await owner.CloseAsync(); }
    }
}
