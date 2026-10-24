using System.Reflection;
using CAN.Models;
using CAN.Runtime;
using CAN.Helpers;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class WriteExecutorLifetimeTests
{
    [Fact]
    public async Task CancelledWriteKeepsNativeOwnerUntilActualExit()
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Plugin = new CanWritePlugin(); state.Setting = new CanWriteSetting { Data = "\"01\"", TimeoutMs = 3000 };
        foreach (string expression in new[] { "\"CAN1\"", "\"0x7DF\"", "\"01\"" }) await ExpressionEvaluatorFactory.Default.EvalStringAsync(expression, context);
        using var release = new ManualResetEventSlim(); var raw = new FakeAdapter { WriteBlock = release };
        var owner = new CanConnectionResource(raw); state.Adapter = owner; using var stop = new CancellationTokenSource();
        var executing = new CAN.Executors.CanWriteExecutor().ExecuteAsync(context, stop.Token);
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(3)); stop.Cancel();
            var result = await executing.WaitAsync(TimeSpan.FromSeconds(2)); Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30)); Assert.False(owner.IsClosed);
        }
        finally
        {
            release.Set(); await executing;
            try { await owner.CloseAsync(3000); } catch when (owner.IsClosed) { }
            Assert.True(owner.IsClosed);
        }
    }

    [Fact]
    public async Task LegacyUnownedRawAdapterIsRejectedBeforeSubmission()
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Plugin = new CanWritePlugin(); state.Setting = new CanWriteSetting { Data = "\"01\"" };
        var raw = new FakeAdapter(); state.Adapter = raw;
        var result = await new CAN.Executors.CanWriteExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Contains("CAN_Open", result.StepResult.Error!.Message);
        Assert.False(raw.WriteEntered.Task.IsCompleted);
    }
}
