using OpcUa.Helpers;
using System.Globalization;
using Opc.Ua;
using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace OpcUa.Tests;

public sealed class SubmissionBoundaryTests
{
    [Theory]
    [InlineData(false, "1.5", true)]
    [InlineData(true, "1.5", true)]
    [InlineData(false, "1,5", false)]
    [InlineData(true, "1,5", false)]
    [InlineData(false, "NaN", false)]
    [InlineData(true, "NaN", false)]
    public async Task ActualAutoWritesUseInvariantFiniteNumbersLikeExplicitTypes(bool batch, string input, bool valid)
    {
        var savedCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        IStepPlugin plugin = batch ? new OpcUaBatchWritePlugin() : new OpcUaWritePlugin();
        string expression = System.Text.Json.JsonSerializer.Serialize(input);
        object setting = batch
            ? new OpcUaBatchWriteSetting { ConnectionName = "\"C\"", Items = [new() { NodeId = "ns=2;s=V", WriteValue = expression, DataType = OpcUaDataType.Auto }] }
            : new OpcUaWriteSetting { ConnectionName = "\"C\"", NodeId = "ns=2;s=V", WriteValue = expression, DataType = OpcUaDataType.Auto };
        var (context, state) = BoundaryContext.Create(plugin, setting);
        var owner = new OpcUaConnectionResource(() => Task.CompletedTask, 1000);
        state.Store.Values["OpcUa_C"] = owner;
        int writes = 0; object? written = null;
        Task<DataValue> Read(NodeId _, CancellationToken token) => Task.FromResult(new DataValue(new Variant(5d)));
        Task<WriteResponse> Write(WriteValueCollection values, CancellationToken token)
        {
            Interlocked.Increment(ref writes); written = values[0].Value.Value;
            return Task.FromResult(new WriteResponse { Results = [StatusCodes.Good] });
        }
        IStepExecutor executor = batch ? new OpcUaBatchWriteExecutor(Read, Write) : new OpcUaWriteExecutor(Read, Write);
        try
        {
            var result = await executor.ExecuteAsync(context);
            Assert.Equal(valid ? TestStatus.Completed : TestStatus.Error, result.StepResult.Status);
            Assert.Equal(valid ? 1 : 0, writes);
            if (valid) Assert.Equal(1.5d, Assert.IsType<double>(written));
        }
        finally { CultureInfo.CurrentCulture = savedCulture; await owner.CloseAsync(2000); }
    }

    [Fact]
    public async Task AcquisitionStopMustWaitForTheConnectionsRealReadNotItsSoftWaiter()
    {
        var owner = new OpcUaConnectionResource(() => Task.CompletedTask, 30);
        var entered = Signal();
        var releaseRead = new TaskCompletionSource<DataValueCollection>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.AcquisitionReader = (_, _) => { entered.TrySetResult(); return releaseRead.Task; };
        var acquisition = owner.StartAcquisition("actual-read", [new() { NodeId = "ns=2;s=V" }], 5, 0, 100);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Delay(100); // 让旧内层软期限结束，而原始 SDK Read 仍明确被 gate 持住。
            await Assert.ThrowsAsync<TimeoutException>(() => acquisition.StopAsync(30));
            Assert.False(acquisition.HasExited);
        }
        finally
        {
            releaseRead.TrySetResult([new(new Variant(5))]);
            await acquisition.StopAsync(2000); acquisition.Dispose(); await owner.CloseAsync(2000);
        }
    }

    [Fact]
    public async Task SubscriptionDeleteCannotReleaseItsActivityBeforeSdkCancelCallbacksExit()
    {
        var owner = new OpcUaConnectionResource(() => Task.CompletedTask, 1000);
        using var releaseCallback = new ManualResetEventSlim();
        var callbackEntered = Signal(); var deleteEntered = Signal(); var releaseDelete = Signal();
        CancellationTokenRegistration registration = default;
        var cleanup = OpcUaSubscriptionCleanup.Start(null, async token =>
        {
            registration = token.Register(() => { callbackEntered.TrySetResult(); releaseCallback.Wait(TimeSpan.FromSeconds(5)); });
            deleteEntered.TrySetResult(); await releaseDelete.Task;
        }, () => { }, owner.EnterActivity(), _ => { }, 80);
        try
        {
            await deleteEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            releaseDelete.TrySetResult();
            await Task.WhenAny(cleanup, Task.Delay(50));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
        }
        finally
        {
            releaseCallback.Set(); releaseDelete.TrySetResult();
            try { await cleanup; } catch { }
            registration.Dispose(); await owner.CloseAsync(2000);
        }
    }

    [Theory]
    [InlineData("deadline")]
    [InlineData("caller")]
    [InlineData("close")]
    public async Task ActualAutoWriteCannotSubmitAfterItsTypeReadOutlivesTheOperation(string stop)
    {
        var plugin = new OpcUaWritePlugin();
        var setting = new OpcUaWriteSetting
        {
            ConnectionName = "\"C\"", NodeId = "ns=2;s=V", WriteValue = "\"7\"",
            DataType = OpcUaDataType.Auto, TimeoutMs = stop == "deadline" ? 80 : 2000
        };
        var (context, state) = BoundaryContext.Create(plugin, setting);
        var owner = new OpcUaConnectionResource(() => Task.CompletedTask, 1000);
        state.Store.Values["OpcUa_C"] = owner;
        using var caller = new CancellationTokenSource();
        var entered = Signal(); var release = new TaskCompletionSource<DataValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        int writes = 0;
        var executor = new OpcUaWriteExecutor((_, _) => { entered.TrySetResult(); return release.Task; }, (_, _) =>
        {
            Interlocked.Increment(ref writes);
            return Task.FromResult(new WriteResponse { Results = [StatusCodes.Good] });
        });
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>(setting.ConnectionName, context);
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>(setting.WriteValue, context);
        var execution = executor.ExecuteAsync(context, caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            if (stop == "caller") caller.Cancel();
            else if (stop == "close") await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
            var result = await execution.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(stop == "caller" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            Assert.True(owner.HasPendingOperation);
            release.TrySetResult(new DataValue(new Variant(5d)));
            await owner.CloseAsync(2000);
            Assert.Equal(0, writes);
        }
        finally
        {
            release.TrySetResult(new DataValue(new Variant(5d)));
            await execution; await owner.CloseAsync(2000);
        }
    }

    [Fact]
    public async Task CallerCancellationCannotRunSdkCallbacksOnTheHostStoppingThread()
    {
        var owner = new OpcUaConnectionResource(() => Task.CompletedTask, 1000);
        using var caller = new CancellationTokenSource();
        using var releaseCallback = new ManualResetEventSlim();
        var entered = Signal(); var callbackEntered = Signal();
        var releaseRead = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = default;
        var request = owner.RunAsync(async token =>
        {
            registration = token.Register(() => { callbackEntered.TrySetResult(); releaseCallback.Wait(TimeSpan.FromSeconds(5)); });
            entered.TrySetResult(); return await releaseRead.Task;
        }, 3000, "SDK cancellation callback", caller.Token);
        Task? cancel = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancel = Task.Run(caller.Cancel);
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Same(cancel, await Task.WhenAny(cancel, Task.Delay(200)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(20));
        }
        finally
        {
            releaseCallback.Set(); releaseRead.TrySetResult(1);
            if (cancel != null) await cancel;
            try { await request; } catch { }
            registration.Dispose(); await owner.CloseAsync(2000);
        }
    }

    [Fact]
    public async Task ActualReadExitDoesNotReleaseSessionWhileItsSdkCancelCallbackStillRuns()
    {
        int closed = 0;
        var owner = new OpcUaConnectionResource(() => { Interlocked.Increment(ref closed); return Task.CompletedTask; }, 1000);
        using var caller = new CancellationTokenSource();
        using var releaseCallback = new ManualResetEventSlim();
        var entered = Signal(); var callbackEntered = Signal(); var readExited = Signal();
        var releaseRead = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = default;
        var request = owner.RunAsync(async token =>
        {
            registration = token.Register(() => { callbackEntered.TrySetResult(); releaseCallback.Wait(TimeSpan.FromSeconds(5)); });
            entered.TrySetResult(); await releaseRead.Task; readExited.TrySetResult(); return 1;
        }, 3000, "read exit before SDK callback", caller.Token);
        Task? cancel = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancel = Task.Run(caller.Cancel);
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            releaseRead.TrySetResult(1);
            await readExited.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
            Assert.Equal(0, closed);
        }
        finally
        {
            releaseCallback.Set(); releaseRead.TrySetResult(1);
            if (cancel != null) await cancel;
            try { await request; } catch { }
            registration.Dispose(); await owner.CloseAsync(2000);
        }
        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task DelayedDeadlineCallbacksCannotTurnAnExpiredRequestIntoSuccess()
    {
        var owner = new OpcUaConnectionResource(() => Task.CompletedTask, 1000);
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var installCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCallback = new ManualResetEventSlim();
        CancellationTokenRegistration registration = default;
        Task<int>? request = null;
        try
        {
            request = owner.RunAsync(async token =>
            {
                await installCallback.Task;
                registration = token.Register(() =>
                {
                    callbackEntered.TrySetResult();
                    if (!releaseCallback.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("test callback scheduler");
                });
                entered.TrySetResult();
                return await releaseRead.Task;
            }, 80, "SDK ignores deadline", default);
            installCallback.TrySetResult();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            releaseRead.TrySetResult(42);
            Assert.Same(request, await Task.WhenAny(request, Task.Delay(300)));
            await Assert.ThrowsAsync<TimeoutException>(() => request);
        }
        finally
        {
            releaseRead.TrySetResult(42); releaseCallback.Set();
            if (request != null) try { await request; } catch { }
            registration.Dispose();
            await owner.CloseAsync(2000);
        }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
