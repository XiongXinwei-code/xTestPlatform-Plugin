using System.Reflection;
using LIN.Adapters;
using LIN.Helpers;
using LIN.Models;
using LIN.Runtime;
using Xunit;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Tests;

public sealed class SleepDeadlineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualSleepStepUsesDeadlineAndCallerCancellation(bool cancel)
    {
        var context = DispatchProxy.Create<IExecutionContext, LinContext>(); var state = (LinContext)(object)context;
        state.Plugin = new LinSleepPlugin(); state.Setting = new LinSleepSetting { ConnectionName = "\"sleep\"", TimeoutMs = cancel ? 3000 : 100, PostSleepDelayMs = 0 };
        await ExpressionEvaluatorFactory.Default.EvalStringAsync("\"sleep\"", context);
        using var release = new ManualResetEventSlim(); var raw = new Driver(release); var owner = new LinConnectionResource(raw);
        state.Items[LinHelper.GetAdapterKey("sleep")] = owner; using var stop = new CancellationTokenSource();
        Task<ExecutionResult>? step = null; bool returned = false;
        var caller = Task.Run(async () => { step = state.Plugin.CreateExecutor().ExecuteAsync(context, stop.Token); returned = true; return await step; });
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); if (cancel) stop.Cancel();
            await Task.Delay(200); Assert.True(returned, "Sleep异步入口不能同步等待原生驱动");
            var result = await caller.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(cancel ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            Assert.False(owner.IsClosed); Assert.Equal(0, raw.CloseCount);
        }
        finally { release.Set(); await caller; await owner.CloseAsync(); }
    }
    private sealed class Driver(ManualResetEventSlim release) : ILinAdapter
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CloseCount; public bool IsConnected => true;
        public void Open(LinAdapterConfig config) { } public void Close() => CloseCount++;
        public void Dispose() => Close(); public void Write(LinFrame frame) { }
        public LinFrame? Read(int timeoutMs, CancellationToken ct = default) => null;
        public LinFrame? Read(byte id, int timeoutMs, CancellationToken ct = default) => null;
        public void Wakeup(bool remote = true) { }
        public void Sleep(bool remote = true) { Entered.TrySetResult(); release.Wait(); }
    }
}
