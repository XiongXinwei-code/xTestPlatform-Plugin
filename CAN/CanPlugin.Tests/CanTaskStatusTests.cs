using System.Reflection;
using System.Text.Json;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanTaskStatusTests
{
    private static CanCyclicDefinition Definition(bool enabled, bool fault = false) => new(0x123, CanFrameType.Standard, false,
        new(enabled, 5), 0, 1000, new(), (_, check) => { check(); if (fault) throw new InvalidOperationException("模拟组帧错误"); return Task.FromResult(new CanMessage { Id = 0x123, Data = [1] }); },
        (_, _) => Task.FromResult(new CanCyclicControl(enabled, 5)), () => { }, () => Task.CompletedTask);

    [Fact]
    public async Task Paused_pool_is_queryable_but_not_reported_as_actively_sending()
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        var context = DispatchProxy.Create<IExecutionContext, DbcDeclaredOutputContext>(); var state = (DbcDeclaredOutputContext)(object)context;
        state.Adapter = owner; state.Plugin = new CanTaskStatusPlugin(); state.DeclareNull("Status", "String"); state.Variables["Locals.Status"] = "";
        var setting = new CanTaskStatusSetting { ResultVariable = "Locals.Status" }; state.Setting = setting;
        try
        {
            owner.UpdateCyclicPool("paused", "CAN", [Definition(false)], () => { });
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Passed, result.StepResult.Status);
            using var all = JsonDocument.Parse((string)state.Variables["Locals.Status"]!);
            var task = Assert.Single(all.RootElement.GetProperty("SendTasks").EnumerateArray());
            Assert.False(task.GetProperty("Active").GetBoolean()); Assert.True(task.GetProperty("Configured").GetBoolean());
            Assert.Equal("Paused", task.GetProperty("Messages")[0].GetProperty("State").GetString());
            setting.IncludeInactive = false;
            Assert.Equal(TestStatus.Passed, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            using var active = JsonDocument.Parse((string)state.Variables["Locals.Status"]!);
            Assert.Empty(active.RootElement.GetProperty("SendTasks").EnumerateArray());
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Reading_fault_status_is_PASS_and_does_not_hide_the_background_error()
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        var context = DispatchProxy.Create<IExecutionContext, DbcDeclaredOutputContext>(); var state = (DbcDeclaredOutputContext)(object)context;
        state.Adapter = owner; state.Plugin = new CanTaskStatusPlugin(); state.DeclareNull("Status", "String"); state.Variables["Locals.Status"] = "";
        state.Setting = new CanTaskStatusSetting { TaskName = "\"fault\"", ResultVariable = "Locals.Status" };
        try
        {
            owner.UpdateCyclicPool("fault", "CAN", [Definition(true, true)], () => { });
            using var timeout = new CancellationTokenSource(5000);
            while (!JsonSerializer.Serialize(owner.GetCyclicStatuses()).Contains("Faulted")) await Task.Delay(5, timeout.Token);
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Passed, result.StepResult.Status);
            using var json = JsonDocument.Parse((string)state.Variables["Locals.Status"]!);
            var task = json.RootElement.GetProperty("SendTasks")[0];
            Assert.False(task.GetProperty("Healthy").GetBoolean()); Assert.Contains("模拟组帧错误", task.GetProperty("Error").GetString());
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData("Locals.Missing", "\"\"")]
    [InlineData("Locals.Number", "\"\"")]
    [InlineData("Locals.Status", "\"missing\"")]
    public async Task Invalid_destination_or_unknown_task_is_Error_without_writing_result(string path, string name)
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        var context = DispatchProxy.Create<IExecutionContext, DbcDeclaredOutputContext>(); var state = (DbcDeclaredOutputContext)(object)context;
        state.Adapter = owner; state.Plugin = new CanTaskStatusPlugin(); state.DeclareNull("Status", "String"); state.Variables["Locals.Status"] = "unchanged"; state.DeclareNull("Number", "Int"); state.Variables["Locals.Number"] = 1;
        state.Setting = new CanTaskStatusSetting { ResultVariable = path, TaskName = name };
        try
        {
            Assert.Equal(TestStatus.Error, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Assert.Equal("unchanged", state.Variables["Locals.Status"]); Assert.False(state.Variables.ContainsKey("Locals.Missing"));
        }
        finally { await owner.CloseAsync(); }
    }
}
