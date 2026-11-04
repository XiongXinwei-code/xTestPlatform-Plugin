using System.Reflection;
using CAN.Dbc;
using CAN.Models;
using CAN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcIndependentCycleTests
{
    [Fact]
    public async Task Multi_ID_auto_and_manual_periods_have_independent_counters_and_native_stop_retires_both()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var setting = new DbcCyclicWriteSetting { UseMessageList = true, CycleTimeMs = 0,
            Document = new() { Text = DbcCycleTimingTests.Text + "\nBA_ \"GenMsgCycleTime\" BO_ 291 15; BA_ \"GenMsgCycleTime\" BO_ 292 500;" },
            Messages = [Row(291, true, 100, 7), Row(292, false, 30, 100)] };
        var context = DispatchProxy.Create<IExecutionContext, PayloadExpressionContext>(); var state = (PayloadExpressionContext)(object)context;
        state.Plugin = new DbcCyclicWritePlugin(); state.Setting = setting; state.Adapter = owner;
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());
            Assert.Contains("0x123=15 ms", result.StepResult.Value?.ToString());
            Assert.Contains("0x124=30 ms", result.StepResult.Value?.ToString());
            // Published configuration is immutable even when the editor/source object changes.
            setting.Messages[0].CycleTimeMs = 0; setting.Messages[0].Protection.RcInitialValue = 200;
            using var deadline = new CancellationTokenSource(5000);
            while (raw.Sent.Count(frame => frame.Id == 292) < 4) await Task.Delay(5, deadline.Token);
            await owner.StopJobAsync("DbcTask1", 2000, default);
            Assert.Equal(new byte[] { 7, 8, 9, 10 }, raw.Sent.Where(frame => frame.Id == 291).Take(4).Select(frame => frame.Data[1]));
            Assert.Equal(new byte[] { 100, 101, 102, 103 }, raw.Sent.Where(frame => frame.Id == 292).Take(4).Select(frame => frame.Data[1]));
            int count = raw.Sent.Count; await Task.Delay(75); Assert.Equal(count, raw.Sent.Count);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Invalid_enabled_row_period_is_rejected_before_job_publication_or_any_frame()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var setting = new DbcCyclicWriteSetting { UseMessageList = true, Document = new() { Text = DbcCycleTimingTests.Text },
            Messages = [Row(291, false, 10, 0), Row(292, false, 0, 0)] };
        var context = DispatchProxy.Create<IExecutionContext, PayloadExpressionContext>(); var state = (PayloadExpressionContext)(object)context;
        state.Plugin = new DbcCyclicWritePlugin(); state.Setting = setting; state.Adapter = owner;
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Empty(raw.Sent);
            setting.Messages[1].Enabled = false;
            var valid = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.True(valid.StepResult.Status == TestStatus.Passed, valid.StepResult.Error?.ToString());
            await owner.StopJobAsync("DbcTask1", 2000, default);
        }
        finally { await owner.CloseAsync(); }
    }

    private static DbcMessageItem Row(uint id, bool automatic, int period, uint counter) => new()
    { MessageId = id, UseDbcCycleTime = automatic, CycleTimeMs = period,
        Protection = new() { RcEnabled = true, RcStartByte = 1, RcEndByte = 1, RcInitialValue = counter } };

    [Fact]
    public async Task Stop_waits_for_actual_DBC_expression_after_wrapper_has_timed_out()
    {
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        using var release = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = DispatchProxy.Create<IExecutionContext, PayloadExpressionContext>(); var state = (PayloadExpressionContext)(object)context;
        state.Variables["Locals.DataIds"] = 12;
        var setting = new DbcCyclicWriteSetting { UseMessageList = false, MessageId = 291, UseDbcCycleTime = false, CycleTimeMs = 100,
            TimeoutMs = 200, Document = new() { Text = DbcCycleTimingTests.Text },
            Signals = [new() { Name = "Value", Value = "Locals.DataIds" }] };
        state.Plugin = new DbcCyclicWritePlugin(); state.Setting = setting; state.Adapter = owner;
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());
            state.BeforeRead = () => { entered.TrySetResult(); release.Wait(); };
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(250);
            await Assert.ThrowsAsync<TimeoutException>(() => owner.StopJobAsync("DbcTask1", 25, default));
            release.Set(); await owner.StopJobAsync("DbcTask1", 2000, default);
            Assert.Empty(raw.Sent);
        }
        finally { release.Set(); await owner.CloseAsync(); }
    }
}
