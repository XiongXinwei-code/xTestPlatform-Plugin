using System.Reflection;
using Opc.Ua;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace OpcUa.Tests;

public sealed class OwnerStopMechanismTests
{
    [Fact]
    public async Task RetiredStepCannotStartCollectorBeforeItsOwnRegistration()
    {
        var entered = Signal(); var release = Signal(); var actualExited = Signal();
        int reads = 0; Exception? constructorFailure = null;
        var (context, _) = BoundaryContext.Create(new OpcUaDataAcqStartPlugin(), new OpcUaDataAcqStartSetting());
        var wrapper = PluginCommon.ExecutionScopedExecutor.Create("OpcUa", () => new CallbackExecutor(async callerToken =>
        {
            entered.TrySetResult(); await release.Task;
            try
            {
                _ = new OpcUaDataAcqTask("late", _ =>
                { Interlocked.Increment(ref reads); return Task.FromResult(new DataValueCollection { new DataValue(5) }); },
                    [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
            }
            catch (Exception ex) { constructorFailure = ex; }
            finally { actualExited.TrySetResult(); }
            return new() { StepResult = new() { Status = TestStatus.Completed } };
        }));
        using var caller = new CancellationTokenSource();
        var waiting = wrapper.ExecuteAsync(context, caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); caller.Cancel();
            Assert.Equal(TestStatus.Aborted, (await waiting.WaitAsync(TimeSpan.FromSeconds(1))).StepResult.Status);
            release.TrySetResult(); await actualExited.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.IsType<InvalidOperationException>(constructorFailure); Assert.Equal(0, reads);
        }
        finally { release.TrySetResult(); await actualExited.Task.WaitAsync(TimeSpan.FromSeconds(3)); await waiting; }
    }

    [Fact]
    public async Task CompletedStartDoesNotTieCollectorToItsFormerStepToken()
    {
        var plugin = new OpcUaDataAcqStartPlugin();
        var (context, state) = BoundaryContext.Create(plugin, new OpcUaDataAcqStartSetting
        { ConnectionName = "\"C\"", TaskName = "\"T\"", SamplingIntervalMs = 10, Items = [new() { NodeId = "ns=2;s=V" }] });
        var firstRead = Signal(); var readAgain = Signal(); int reads = 0;
        var connection = new OpcUaConnectionResource(() => Task.CompletedTask, 3000)
        {
            AcquisitionReader = (_, _) =>
            {
                int count = Interlocked.Increment(ref reads);
                if (count == 1) firstRead.TrySetResult();
                else readAgain.TrySetResult();
                return Task.FromResult(new DataValueCollection { new DataValue(5) });
            }
        };
        state.Store.Values["OpcUa_C"] = connection;
        using var caller = new CancellationTokenSource();
        try
        {
            Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(context, caller.Token)).StepResult.Status);
            var collector = Assert.IsType<OpcUaDataAcqTask>(state.Store.Values["OpcUaDataAcq_T"]);
            caller.Cancel();
            await firstRead.Task.WaitAsync(TimeSpan.FromSeconds(3)); await readAgain.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(collector.IsStopRequested); Assert.False(connection.IsClosed);
        }
        finally { await connection.CloseAsync(3000); }
    }

    [Fact]
    public async Task FreezeRetainsCancellationCallbackFailureAfterActualConsumerLeaves()
    {
        int reads = 0;
        var collected = Signal(); var entered = Signal(); var actualExit = Signal();
        var owner = new OpcUaDataAcqTask("callback", async token =>
        {
            if (Interlocked.Increment(ref reads) == 1) return new() { new DataValue(5) };
            collected.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return new();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
        CancellationTokenRegistration registration = default;
        Task<int>? reading = null;
        try
        {
            await collected.Task.WaitAsync(TimeSpan.FromSeconds(3));
            reading = owner.ConsumeWithBudgetAsync(-1, async (_, operation) =>
            {
                registration = operation.Token.Register(() => throw new InvalidOperationException("native consumer cancellation failed"));
                entered.TrySetResult(); await actualExit.Task; return 1;
            }, 30000, default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            owner.RequestStop();
            await Assert.ThrowsAsync<InvalidOperationException>(() => reading.WaitAsync(TimeSpan.FromSeconds(1)));
            actualExit.TrySetResult();
            using var deadline = new CancellationTokenSource(3000);
            while (owner.PendingConsumers != 0) await Task.Delay(5, deadline.Token);
            Assert.Contains("native consumer cancellation failed", owner.LastConsumptionError);
            Assert.Equal(1, owner.SampleCount);
        }
        finally
        {
            actualExit.TrySetResult();
            if (reading != null) try { await reading.WaitAsync(TimeSpan.FromSeconds(3)); } catch (Exception) when (reading.IsCompleted) { }
            await owner.StopAsync(3000); owner.Dispose(); await owner.CloseCompletion.WaitAsync(TimeSpan.FromSeconds(3)); registration.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreezeEndsActiveAndQueuedObserversBeforeActualPreparationExits(bool committedOutput)
    {
        int reads = 0, queuedEntered = 0, published = 0;
        var collected = Signal(); var entered = Signal(); var actualExit = Signal();
        var owner = new OpcUaDataAcqTask("T", async token =>
        {
            if (Interlocked.Increment(ref reads) == 1) return new() { new DataValue(5) };
            collected.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return new();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
        Task<int>? first = null, queued = null;
        try
        {
            await collected.Task.WaitAsync(TimeSpan.FromSeconds(3));
            first = owner.ConsumeWithBudgetAsync(-1, async (records, operation) =>
            {
                entered.TrySetResult(); await actualExit.Task;
                if (committedOutput) operation.MarkExternalOutputCommitted();
                return records.Count;
            }, 30000, default, _ => Interlocked.Increment(ref published));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            queued = owner.ConsumeWithBudgetAsync(-1, (records, _) =>
            { Interlocked.Increment(ref queuedEntered); return Task.FromResult(records.Count); }, 30000, default);
            owner.RequestStop();
            await Assert.ThrowsAsync<InvalidOperationException>(() => first.WaitAsync(TimeSpan.FromSeconds(1)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => queued.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(owner.Completion.IsCompleted); Assert.Equal(1, owner.SampleCount);
            Assert.Equal(0, published); Assert.Equal(0, queuedEntered);
            actualExit.TrySetResult();
            await owner.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(committedOutput ? 0 : 1, owner.SampleCount);
            Assert.Equal(committedOutput ? 1 : 0, owner.LastExportCommitSequence);
            Assert.Equal(0, published); Assert.Equal(0, queuedEntered);
        }
        finally
        {
            actualExit.TrySetResult();
            if (first != null) try { await first.WaitAsync(TimeSpan.FromSeconds(3)); } catch (Exception) when (first.IsCompleted) { }
            if (queued != null) try { await queued.WaitAsync(TimeSpan.FromSeconds(3)); } catch (Exception) when (queued.IsCompleted) { }
            await owner.StopAsync(3000); owner.Dispose(); await owner.CloseCompletion.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    [Theory]
    [InlineData("CleanupExecution")]
    [InlineData("CleanupAll")]
    public async Task ActualSdkCleanupEndsReadObserverWhileCsvAndAcquisitionRemainOwned(string cleanup)
    {
        var registry = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        int reads = 0, exports = 0;
        var collected = Signal(); var exporting = Signal(); var exportExit = Signal();
        var task = new OpcUaDataAcqTask("T", async token =>
        {
            if (Interlocked.Increment(ref reads) == 1) return new() { new DataValue(5) };
            collected.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return new();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
        task.CsvExporter = async (_, _, _) => { Interlocked.Increment(ref exports); exporting.TrySetResult(); await exportExit.Task; };
        var plugin = new OpcUaDataAcqReadPlugin();
        var (context, state) = BoundaryContext.Create(plugin, new OpcUaDataAcqReadSetting
        { TaskName = "\"T\"", TimeoutMs = 30000, SaveToFile = true, CsvFilePath = "\"owner-stop-" + Guid.NewGuid().ToString("N") + ".csv\"", ResultVariable = "Locals.Wave" });
        state.UseRegistry(registry); Assert.True(registry.TryAdd("OpcUaDataAcq_T", task));
        var execution = plugin.CreateExecutor().ExecuteAsync(context);
        try
        {
            await collected.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await exporting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            registry.GetType().GetMethod(cleanup, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
            var result = await execution.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("结束等待", result.StepResult.Error!.Message);
            Assert.False(task.CloseCompletion.IsCompleted); Assert.Equal(1, task.SampleCount); Assert.Equal(1, exports);
            exportExit.TrySetResult();
            await task.CloseCompletion.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(0, task.SampleCount); Assert.Equal(1, task.LastExportCommitSequence); Assert.Equal(1, exports);
        }
        finally
        {
            exportExit.TrySetResult(); task.Dispose(); await task.CloseCompletion.WaitAsync(TimeSpan.FromSeconds(3));
            await execution.WaitAsync(TimeSpan.FromSeconds(3));
            registry.GetType().GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);
        }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class CallbackExecutor(Func<CancellationToken, Task<ExecutionResult>> action) : IStepExecutor
    {
        public Task<ExecutionResult> ExecuteAsync(xTestPlatform.Core.Engine.IExecutionContext context, CancellationToken cancellationToken = default)
            => action(cancellationToken);
    }
}
