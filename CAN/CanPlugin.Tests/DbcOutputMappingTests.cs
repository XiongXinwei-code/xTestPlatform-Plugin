using System.Reflection;
using System.Text.Json;
using CAN.Dbc;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcOutputMappingTests
{
    private static DbcDocumentSetting Document() => new() { Name = "outputs.dbc", Text = """
        BO_ 291 First: 1 ECU
         SG_ Value : 0|8@1+ (0.5,10) [10|137.5] "V" ECU
        BO_ 292 Second: 1 ECU
         SG_ Value : 0|8@1- (1,0) [-128|127] "A" ECU
        BO_ 293 Mux: 2 ECU
         SG_ Selector M : 0|8@1+ (1,0) [0|255] "" ECU
         SG_ Branch m1 : 8|8@1+ (1,0) [0|255] "" ECU
        """ };
    private static DbcMessageItem Row(uint id, string path, DbcValueKind kind = DbcValueKind.Physical) => new()
    { MessageId = id, SignalOutputs = [new() { Name = "Value", TargetVariable = path, ValueKind = kind }] };
    private static (IExecutionContext Context, DbcOutputTestContext State) Context(DbcReadSetting setting, CanConnectionResource owner)
    {
        var context = DispatchProxy.Create<IExecutionContext, DbcOutputTestContext>();
        var state = (DbcOutputTestContext)(object)context;
        state.Plugin = setting is DbcCaptureSetting ? new DbcCapturePlugin() : new DbcReadPlugin();
        state.Setting = setting; state.Adapter = owner;
        state.Variables["Locals.Json"] = "unchanged";
        return (context, state);
    }
    private static CanReceiveRouter Router(CanConnectionResource owner) => (CanReceiveRouter)typeof(CanConnectionResource)
        .GetField("_router", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
    private static async Task Ready(CanReceiveRouter router)
    {
        using var deadline = new CancellationTokenSource(5000);
        while (router.CaptureCount == 0) await Task.Delay(5, deadline.Token);
    }
    private static void Passed(ExecutionResult result) => Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_matches_ID_and_signal_name_and_supports_mapping_only_or_also_JSON(bool withJson)
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcReadSetting { Document = Document(), UseMessageList = true,
            ResultVariable = withJson ? "Locals.Json" : "", Messages = [Row(291, "Locals.Voltage"), Row(292, "Locals.Raw", DbcValueKind.Raw)] };
        var (context, state) = Context(setting, owner);
        state.Variables["Locals.Voltage"] = -1d; state.Variables["Locals.Raw"] = "old";
        try
        {
            var task = state.Plugin.CreateExecutor().ExecuteAsync(context); await Ready(router);
            router.Publish(new() { Id = 292, Data = [254] });
            router.Publish(new() { Id = 291, Data = [5] });
            Passed(await task);
            Assert.Equal(12.5d, state.Variables["Locals.Voltage"]); Assert.Equal("-2", state.Variables["Locals.Raw"]);
            if (withJson) { using var json = JsonDocument.Parse((string)state.Variables["Locals.Json"]!); Assert.Equal(2, json.RootElement.GetProperty("count").GetInt32()); }
            else Assert.Equal("unchanged", state.Variables["Locals.Json"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Capture_replaces_lists_in_per_ID_receive_order_and_empty_window_clears_old_samples(bool empty)
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcCaptureSetting { Document = Document(), UseMessageList = true, DurationMs = 150, MaxFrames = 3,
            Messages = [Row(291, "Locals.Voltage"), Row(292, "Locals.Raw", DbcValueKind.Raw)] };
        var (context, state) = Context(setting, owner);
        var original = new List<double> { -1 }; state.Variables["Locals.Voltage"] = original; state.Variables["Locals.Raw"] = new long[] { 99 };
        try
        {
            var task = state.Plugin.CreateExecutor().ExecuteAsync(context); await Ready(router);
            if (!empty) { router.Publish(new() { Id = 291, Data = [5] }); router.Publish(new() { Id = 292, Data = [254] }); router.Publish(new() { Id = 291, Data = [7] }); }
            Passed(await task);
            Assert.Equal(empty ? [] : new[] { 12.5, 13.5 }, Assert.IsType<List<double>>(state.Variables["Locals.Voltage"]));
            Assert.Equal(empty ? [] : new long[] { -2 }, Assert.IsType<long[]>(state.Variables["Locals.Raw"]));
            Assert.Equal(new[] { -1d }, original); Assert.Equal("unchanged", state.Variables["Locals.Json"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inactive_mux_read_fails_without_publication_while_capture_omits_inactive_samples(bool capture)
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        DbcReadSetting setting = capture ? new DbcCaptureSetting { DurationMs = 100, MaxFrames = 2 } : new DbcReadSetting();
        setting.Document = Document(); setting.UseMessageList = false; setting.MessageId = 293; setting.SignalOutputs = [new() { Name = "Branch", TargetVariable = "Locals.Branch" }];
        var (context, state) = Context(setting, owner); state.Variables["Locals.Branch"] = capture ? new List<int> { 99 } : 99;
        try
        {
            var task = state.Plugin.CreateExecutor().ExecuteAsync(context); await Ready(router);
            router.Publish(new() { Id = 293, Data = [0, 200] });
            if (capture) router.Publish(new() { Id = 293, Data = [1, 42] });
            var result = await task;
            if (capture) { Passed(result); Assert.Equal(new[] { 42 }, Assert.IsType<List<int>>(state.Variables["Locals.Branch"])); }
            else { Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(99, state.Variables["Locals.Branch"]); }
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("case")]
    [InlineData("json")]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("list")]
    [InlineData("all")]
    public async Task Invalid_destination_contract_is_rejected_before_receive(string defect)
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        DbcReadSetting setting = defect is "list" or "all" ? new DbcCaptureSetting { AllDbcMessages = defect == "all" } : new DbcReadSetting();
        setting.Document = Document(); setting.UseMessageList = false; setting.MessageId = 291; setting.TimeoutMs = 60000;
        setting.SignalOutputs = [new() { Name = "Value", TargetVariable = defect == "json" ? "Locals.Json" : "Locals.Output" }];
        var (context, state) = Context(setting, owner); state.Variables["Locals.Output"] = 0d;
        if (defect == "missing") state.Variables.Remove("Locals.Output");
        if (defect == "unknown") setting.SignalOutputs[0].Name = "Missing";
        if (defect == "json") setting.ResultVariable = "Locals.Json";
        if (defect is "duplicate" or "case") { setting.UseMessageList = true; setting.Messages = [Row(291, "Locals.Output"), Row(292, defect == "case" ? "locals.output" : "Locals.Output")]; }
        if (defect == "case") state.Variables["locals.output"] = 0d;
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(0, router.CaptureCount);
            if (defect is "duplicate" or "case" or "json") Assert.Contains("重复", result.StepResult.Error!.Message);
            Assert.Empty(state.Writes); Assert.Equal("unchanged", state.Variables["Locals.Json"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Conversion_error_does_not_publish_JSON_or_other_valid_signals()
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcReadSetting { Document = Document(), UseMessageList = true, ResultVariable = "Locals.Json",
            Messages = [Row(292, "Locals.Good"), Row(291, "Locals.Int")] };
        var (context, state) = Context(setting, owner); state.Variables["Locals.Good"] = 99d; state.Variables["Locals.Int"] = 99;
        try
        {
            var task = state.Plugin.CreateExecutor().ExecuteAsync(context); await Ready(router);
            router.Publish(new() { Id = 292, Data = [2] }); router.Publish(new() { Id = 291, Data = [5] });
            Assert.Equal(TestStatus.Error, (await task).StepResult.Status); Assert.Empty(state.Writes);
            Assert.Equal("unchanged", state.Variables["Locals.Json"]); Assert.Equal(99d, state.Variables["Locals.Good"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public void Raw_64_bit_values_remain_exact_and_integer_fraction_or_overflow_are_rejected()
    {
        Assert.Equal(ulong.MaxValue, DbcVariableBinding.ConvertValues([ulong.MaxValue], typeof(ulong), false, DbcValueKind.Raw));
        Assert.Equal("18446744073709551615", DbcVariableBinding.ConvertValues([ulong.MaxValue], typeof(string), false, DbcValueKind.Raw));
        Assert.Throws<ArgumentException>(() => DbcVariableBinding.ConvertValues([ulong.MaxValue], typeof(double), false, DbcValueKind.Raw));
        Assert.Throws<ArgumentException>(() => DbcVariableBinding.ConvertValues([1.5m], typeof(int), false, DbcValueKind.Physical));
        Assert.Throws<OverflowException>(() => DbcVariableBinding.ConvertValues([256], typeof(byte), false, DbcValueKind.Raw));
    }

    [Fact]
    public async Task Cancellation_before_completion_never_publishes_and_rerun_requires_new_samples()
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcReadSetting { Document = Document(), UseMessageList = true, Messages = [Row(291, "Locals.A"), Row(292, "Locals.B")] };
        var (context, state) = Context(setting, owner); state.Variables["Locals.A"] = 0d; state.Variables["Locals.B"] = 0d;
        try
        {
            using var stop = new CancellationTokenSource();
            var task = state.Plugin.CreateExecutor().ExecuteAsync(context, stop.Token); await Ready(router);
            router.Publish(new() { Id = 291, Data = [5] }); stop.Cancel();
            Assert.Equal(TestStatus.Aborted, (await task).StepResult.Status); Assert.Empty(state.Writes); Assert.Equal(0, router.CaptureCount);
            task = state.Plugin.CreateExecutor().ExecuteAsync(context); await Ready(router);
            router.Publish(new() { Id = 292, Data = [4] }); await Task.Delay(20); Assert.False(task.IsCompleted);
            router.Publish(new() { Id = 291, Data = [8] }); Passed(await task);
            Assert.Equal(14d, state.Variables["Locals.A"]); Assert.Equal(4d, state.Variables["Locals.B"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Setter_failure_reports_confirmed_and_unknown_paths_without_overwriting_later_results()
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcReadSetting { Document = Document(), UseMessageList = true, ResultVariable = "Locals.Json",
            Messages = [Row(291, "Locals.A"), Row(292, "Locals.B")] };
        var (context, state) = Context(setting, owner); state.Variables["Locals.A"] = 99d; state.Variables["Locals.B"] = 99d;
        state.AfterWrite = path => { if (path == "Locals.A") throw new InvalidOperationException("setter failed after mutation"); };
        try
        {
            var task = state.Plugin.CreateExecutor().ExecuteAsync(context); await Ready(router);
            router.Publish(new() { Id = 291, Data = [4] }); router.Publish(new() { Id = 292, Data = [2] });
            var result = await task; Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("已确认写入：Locals.Json", result.StepResult.Error!.Message);
            Assert.Contains("结果未确认：Locals.A", result.StepResult.Error!.Message);
            Assert.Equal(99d, state.Variables["Locals.B"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Cancellation_inside_setter_stops_remaining_publications()
    {
        var owner = new CanConnectionResource(new FakeAdapter()); var router = Router(owner);
        var setting = new DbcReadSetting { Document = Document(), UseMessageList = true,
            Messages = [Row(291, "Locals.A"), Row(292, "Locals.B")] };
        var (context, state) = Context(setting, owner); state.Variables["Locals.A"] = 99d; state.Variables["Locals.B"] = 99d;
        using var cancel = new CancellationTokenSource(); state.AfterWrite = _ => cancel.Cancel();
        try
        {
            var task = state.Plugin.CreateExecutor().ExecuteAsync(context, cancel.Token); await Ready(router);
            router.Publish(new() { Id = 291, Data = [4] }); router.Publish(new() { Id = 292, Data = [2] });
            Assert.Equal(TestStatus.Aborted, (await task).StepResult.Status);
            Assert.Equal(new[] { "Locals.A" }, state.Writes); Assert.Equal(99d, state.Variables["Locals.B"]);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public void Current_format_preserves_JSON_document_and_per_ID_output_mapping()
    {
        var current = new DbcReadSetting { Document = Document(), ResultVariable = "Locals.Json",
            Messages = [new() { MessageId = 291, SignalOutputs = [new() { Name = "Value", TargetVariable = "Locals.Value" }] }] };
        var serializer = new DbcReadPlugin().CreateSerializer();
        var restored = Assert.IsType<DbcReadSetting>(serializer.Deserialize(serializer.Serialize(current), serializer.SettingVersion));
        Assert.Equal(new Step().StepSetting.SettingVersion, serializer.SettingVersion); Assert.Equal("Locals.Json", restored.ResultVariable);
        Assert.Equal(Document().Name, restored.Document.Name); Assert.Equal(Document().Text, restored.Document.Text);
        Assert.True(restored.UseMessageList); Assert.Equal(291u, Assert.Single(restored.Messages).MessageId);
        Assert.Equal("Locals.Value", Assert.Single(restored.Messages[0].SignalOutputs).TargetVariable);
    }
}

public class DbcOutputTestContext : PayloadExpressionContext
{
    public List<string> Writes { get; } = [];
    public Action<string>? AfterWrite { get; set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var result = base.Invoke(method, args);
        if (method!.Name == "SetVariable") { string path = (string)args![0]!; Writes.Add(path); AfterWrite?.Invoke(path); }
        return result;
    }
}
