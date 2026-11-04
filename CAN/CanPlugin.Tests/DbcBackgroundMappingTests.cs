using System.Reflection;
using System.Text.Json;
using CAN.Dbc;
using CAN.Models;
using xTestPlatform.Core.Engine;
using Xunit;

namespace CAN.Tests;

public sealed class DbcBackgroundMappingTests
{
    private const string Text = """
        BO_ 291 First: 2 ECU
         SG_ Value : 0|8@1+ (0.5,10) [10|137.5] "V" ECU
         SG_ Counter : 8|8@1+ (1,0) [0|255] "" ECU
        BO_ 292 Second: 2 ECU
         SG_ Value : 0|8@1- (1,0) [-128|127] "A" ECU
         SG_ Counter : 8|8@1+ (1,0) [0|255] "" ECU
        BO_ 293 Mux: 2 ECU
         SG_ Selector M : 0|8@1+ (1,0) [0|255] "" ECU
         SG_ Branch m1 : 8|8@1+ (1,0) [0|255] "" ECU
        """;
    private static (IExecutionContext Context, DbcOutputTestContext State) Context()
    {
        var context = DispatchProxy.Create<IExecutionContext, DbcOutputTestContext>();
        var state = (DbcOutputTestContext)(object)context;
        state.Variables["Locals.Json"] = "old"; state.Variables["Locals.First"] = -1d;
        state.Variables["Locals.Second"] = "old"; state.Variables["Locals.Branch"] = -1;
        return (context, state);
    }
    private static DbcSelectedMessage Select(uint id, params DbcSignalOutput[] outputs) =>
        new(DbcDatabase.Parse(Text).Find(id, false), DbcFrameFormat.Classic, new(), [], 0, outputs);
    private static DbcBackgroundFrame Sample(uint id, long sequence, byte[] data, bool valid = true, string quality = "Valid") =>
        new(id, CanFrameType.Standard, false, new() { Id = id, Data = data }, sequence, valid, quality, valid ? 1 : 2000);

    [Fact]
    public async Task Latest_per_ID_maps_physical_or_raw_and_does_not_republish_same_sequence()
    {
        var (context, state) = Context();
        var selected = new[] { Select(291, new DbcSignalOutput { Name = "Value", TargetVariable = "Locals.First" }, new DbcSignalOutput { Name = "Counter", Selected = false }),
            Select(292, new DbcSignalOutput { Name = "Value", TargetVariable = "Locals.Second", ValueKind = DbcValueKind.Raw }) };
        var mapper = new DbcReadBackgroundMapping(new() { ResultVariable = "Locals.Json" }, selected, "hash", context);
        DbcBackgroundFrame[] samples = [Sample(291, 101, [5, 3]), Sample(292, 201, [254, 4])];
        var values = await mapper.PrepareAsync(samples, default);
        Assert.Equal(12.5d, values["Locals.First"]); Assert.Equal("-2", values["Locals.Second"]);
        using var json = JsonDocument.Parse(Assert.IsType<string>(values["Locals.Json"]));
        Assert.True(json.RootElement.GetProperty("allValid").GetBoolean());
        var first = json.RootElement.GetProperty("frames")[0];
        Assert.Equal(101, first.GetProperty("sequence").GetInt64());
        Assert.Equal("Value", Assert.Single(first.GetProperty("frame").GetProperty("signals").EnumerateArray()).GetProperty("name").GetString());
        mapper.Published(samples);
        var unchanged = await mapper.PrepareAsync(samples, default);
        Assert.Single(unchanged); Assert.True(unchanged.ContainsKey("Locals.Json"));
        var next = await mapper.PrepareAsync([Sample(291, 110, [9, 7]), samples[1]], default);
        Assert.Equal(14.5d, next["Locals.First"]); Assert.False(next.ContainsKey("Locals.Second"));
        Assert.Empty(state.Writes); // Preparing never mutates platform variables.
    }

    [Fact]
    public async Task No_data_stale_and_inactive_mux_retain_scalar_and_publish_explicit_diagnostics()
    {
        var (context, _) = Context();
        var mapper = new DbcReadBackgroundMapping(new() { ResultVariable = "Locals.Json" },
            [Select(293, new DbcSignalOutput { Name = "Branch", TargetVariable = "Locals.Branch" })], "hash", context);
        var noData = await mapper.PrepareAsync([], default); Assert.Single(noData);
        using (var json = JsonDocument.Parse(Assert.IsType<string>(noData["Locals.Json"])))
            Assert.Equal("NoData", json.RootElement.GetProperty("frames")[0].GetProperty("quality").GetString());
        var inactive = await mapper.PrepareAsync([Sample(293, 1, [0, 55])], default); Assert.Single(inactive);
        using (var json = JsonDocument.Parse(Assert.IsType<string>(inactive["Locals.Json"])))
            Assert.Equal("InactiveMultiplexer", json.RootElement.GetProperty("frames")[0].GetProperty("mappings")[0].GetProperty("reason").GetString());
        var stale = await mapper.PrepareAsync([Sample(293, 2, [1, 66], false, "Stale")], default); Assert.Single(stale);
        using (var json = JsonDocument.Parse(Assert.IsType<string>(stale["Locals.Json"])))
            Assert.False(json.RootElement.GetProperty("allValid").GetBoolean());
        var good = await mapper.PrepareAsync([Sample(293, 3, [1, 77])], default); Assert.Equal(77, good["Locals.Branch"]);
    }

    [Fact]
    public async Task Short_frames_and_conversion_failure_never_return_partial_updates()
    {
        var (context, state) = Context();
        var mapper = new DbcReadBackgroundMapping(new() { ResultVariable = "Locals.Json" },
            [Select(291, new DbcSignalOutput { Name = "Value", TargetVariable = "Locals.First" }), Select(292)], "hash", context);
        await Assert.ThrowsAnyAsync<Exception>(() => mapper.PrepareAsync([Sample(291, 1, [6, 0]), Sample(292, 1, [])], default));
        Assert.Empty(state.Writes);
        state.Variables["Locals.First"] = 0;
        var integerMapper = new DbcReadBackgroundMapping(new(), [Select(291, new DbcSignalOutput { Name = "Value", TargetVariable = "Locals.First" })], "hash", context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => integerMapper.PrepareAsync([Sample(291, 2, [5, 0])], default));
        Assert.Empty(state.Writes);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("json")]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("none")]
    public void Invalid_output_contract_is_rejected_before_background_start(string defect)
    {
        var (context, _) = Context();
        var settings = new DbcReadBackgroundSetting { ResultVariable = defect == "none" ? "" : "Locals.Json" };
        var selected = defect switch
        {
            "duplicate" => new[] { Select(291, new DbcSignalOutput { Name = "Value", TargetVariable = "Locals.First" }), Select(292, new DbcSignalOutput { Name = "Value", TargetVariable = "Locals.First" }) },
            "json" => [Select(291, new DbcSignalOutput { Name = "Value", TargetVariable = "Locals.Json" })],
            "missing" => [Select(291, new DbcSignalOutput { Name = "Value", TargetVariable = "Locals.Missing" })],
            "unknown" => [Select(291, new DbcSignalOutput { Name = "Missing", TargetVariable = "Locals.First" })],
            _ => [Select(291)]
        };
        Assert.Throws<ArgumentException>(() => new DbcReadBackgroundMapping(settings, selected, "hash", context));
    }

    [Fact]
    public async Task Deselected_mapping_does_not_require_a_variable_and_cancellation_does_not_prepare_updates()
    {
        var (context, state) = Context();
        var mapper = new DbcReadBackgroundMapping(new() { ResultVariable = "Locals.Json" },
            [Select(291, new DbcSignalOutput { Name = "Value", TargetVariable = "Locals.Missing", Selected = false })], "hash", context);
        var result = await mapper.PrepareAsync([Sample(291, 1, [3, 4])], default); Assert.Single(result);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mapper.PrepareAsync([Sample(291, 2, [5, 6])], new CancellationToken(true)));
        Assert.Empty(state.Writes);
    }

    [Fact]
    public async Task Unknown_capture_time_updates_new_payload_but_never_claims_freshness()
    {
        var (context, _) = Context();
        var mapper = new DbcReadBackgroundMapping(new() { ResultVariable = "Locals.Json" },
            [Select(291, new DbcSignalOutput { Name = "Value", TargetVariable = "Locals.First" })], "hash", context);
        DbcBackgroundFrame[] samples = [Sample(291, 1, [5, 1], false, "TimeUnknown")];
        var result = await mapper.PrepareAsync(samples, default);
        Assert.Equal(12.5d, result["Locals.First"]);
        using var json = JsonDocument.Parse((string)result["Locals.Json"]!);
        Assert.False(json.RootElement.GetProperty("allValid").GetBoolean());
        Assert.Equal("TimeUnknown", json.RootElement.GetProperty("frames")[0].GetProperty("quality").GetString());
        mapper.Published(samples);
        Assert.Single(await mapper.PrepareAsync(samples, default));
    }

    [Fact]
    public async Task Bad_CRC_is_preserved_as_diagnostic_and_does_not_hide_valid_signal_data()
    {
        var (context, _) = Context();
        var selected = Select(291, new DbcSignalOutput { Name = "Value", TargetVariable = "Locals.First" }) with
        {
            Protection = new() { CrcEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Crc8,
                CrcStartByte = 1, CrcEndByte = 1, CrcDataStartByte = 0, CrcDataEndByte = 0 }
        };
        byte[] payload = new CAN.Helpers.CanPayloadProcessor(selected.Protection).Prepare([5, 0]); payload[1] ^= 1;
        var mapper = new DbcReadBackgroundMapping(new() { ResultVariable = "Locals.Json" }, [selected], "hash", context);
        var result = await mapper.PrepareAsync([Sample(291, 1, payload)], default);
        Assert.Equal(12.5d, result["Locals.First"]);
        using var json = JsonDocument.Parse((string)result["Locals.Json"]!);
        Assert.False(json.RootElement.GetProperty("frames")[0].GetProperty("frame").GetProperty("integrity").GetProperty("crcValid").GetBoolean());
    }
}
