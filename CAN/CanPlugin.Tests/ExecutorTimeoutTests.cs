using System.Diagnostics;
using System.Reflection;
using CAN.Models;
using CAN.Helpers;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class ExecutorTimeoutTests
{
    [Fact]
    public async Task Configured_close_timeout_is_not_replaced_by_fixed_five_seconds()
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Setting = new CanCloseSetting { TimeoutMs = 37 };
        await ExpressionEvaluatorFactory.Default.EvalStringAsync("\"CAN1\"", context);
        using var exit = new ManualResetEventSlim();
        var raw = new FakeAdapter { WriteBlock = exit }; var owner = new CanConnectionResource(raw); state.Adapter = owner;
        var writing = Task.Run(() => owner.Write(new() { Id = 0x123, Data = [1] }));
        await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            var clock = Stopwatch.StartNew();
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("37", result.StepResult.Error!.Detail);
            Assert.True(clock.ElapsedMilliseconds < 1200, $"elapsed={clock.ElapsedMilliseconds}");
            Assert.False(owner.IsClosed); Assert.Equal(0, state.RemoveCount);
        }
        finally { exit.Set(); await writing; await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Close_freezes_captured_owner_even_if_registry_gate_wait_times_out()
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Setting = new CanCloseSetting { TimeoutMs = 40 };
        await ExpressionEvaluatorFactory.Default.EvalStringAsync("\"CAN1\"", context);
        var owner = new CanConnectionResource(new FakeAdapter()); state.Adapter = owner;
        using (await CanRegistryGuard.EnterAsync(context.Resources, "CAN1", default))
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.True(owner.IsClosing);
            Assert.Throws<InvalidOperationException>(() => owner.Write(new() { Id = 0x123, Data = [1] }));
        }
        await owner.CloseAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Corrupt_settings_return_diagnostic_error_inside_executor(bool open)
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Plugin = open ? new CanOpenPlugin() : new CanClosePlugin(); state.SettingBytes = [0xc1];
        var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.NotEmpty(result.StepResult.Error!.Detail!);
    }
}
