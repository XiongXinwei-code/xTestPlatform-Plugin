using Opc.Ua;
using OpcUa.Helpers;
using OpcUa.Models;
using System.Collections.Concurrent;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace OpcUa.Tests;

public sealed class AcquisitionBudgetTests
{
    [Fact]
    public async Task CallerCancellationEndsWaitButKeepsActualConsumerAndUncommittedFifo()
    {
        var (task, ready) = CreateOneRecord();
        var entered = Signal();
        var release = Signal();
        using var caller = new CancellationTokenSource();
        Task<int>? consuming = null;
        Task<int>? next = null;
        int nextEntered = 0;
        try
        {
            await ready.WaitAsync(TimeSpan.FromSeconds(3));
            consuming = task.ConsumeAsync(-1, async _ => { entered.SetResult(); await release.Task; return 1; }, caller.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            caller.Cancel();
            Assert.Same(consuming, await Task.WhenAny(consuming, Task.Delay(200)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consuming);
            next = task.ConsumeAsync(-1, records => { Interlocked.Increment(ref nextEntered); return Task.FromResult(records.Count); }, default);
            await Task.Delay(40);
            Assert.Equal(0, nextEntered);
            Assert.Equal(1, task.SampleCount);
            release.SetResult();
            Assert.Equal(1, await next.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        finally
        {
            release.TrySetResult();
            if (consuming != null) try { await consuming; } catch (OperationCanceledException) { }
            if (next != null) await next;
            await task.StopAsync(1000); task.Dispose();
        }
    }

    [Fact]
    public async Task StopBudgetIncludesSlowCancellationCallbacksWithoutBlockingCaller()
    {
        var callbackEntered = Signal();
        using var release = new ManualResetEventSlim();
        var reading = Signal();
        var task = new OpcUaDataAcqTask("cancel-callback", async ct =>
        {
            using var registered = ct.Register(() => { callbackEntered.TrySetResult(); release.Wait(TimeSpan.FromSeconds(5)); });
            reading.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return new DataValueCollection();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
        Task<List<DataAcqRecord>>? stopping = null;
        try
        {
            await reading.Task.WaitAsync(TimeSpan.FromSeconds(2));
            stopping = Task.Run(() => task.StopAsync(30));
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Same(stopping, await Task.WhenAny(stopping, Task.Delay(200)));
            await Assert.ThrowsAsync<TimeoutException>(() => stopping);
        }
        finally
        {
            release.Set();
            if (stopping != null) try { await stopping; } catch (TimeoutException) { }
            await task.StopAsync(1000); task.Dispose();
        }
    }

    [Fact]
    public async Task LifetimeCancellationNeverRunsBlockingReadCallbacksOnItsCaller()
    {
        using var lifetime = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var reading = Signal(); var callbackEntered = Signal();
        var task = new OpcUaDataAcqTask("lifetime-callback", async ct =>
        {
            using var registered = ct.Register(() => { callbackEntered.TrySetResult(); release.Wait(TimeSpan.FromSeconds(5)); });
            reading.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return new();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100, lifetime.Token);
        Task? cancelling = null;
        try
        {
            await reading.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancelling = Task.Run(lifetime.Cancel);
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Same(cancelling, await Task.WhenAny(cancelling, Task.Delay(200)));
            await Assert.ThrowsAsync<TimeoutException>(() => task.StopAsync(20));
            Assert.False(task.HasExited);
        }
        finally { release.Set(); if (cancelling != null) await cancelling; await task.StopAsync(1000); task.Dispose(); }
    }

    [Fact]
    public async Task CallbackFailureIsDiagnosedWithoutMisreportingActualExitAsPendingForever()
    {
        var reading = Signal();
        var task = new OpcUaDataAcqTask("callback-fault", async ct =>
        {
            using var registered = ct.Register(() => throw new InvalidOperationException("SDK cancel callback failed"));
            reading.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return new();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
        try
        {
            await reading.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await task.StopAsync(1000);
            Assert.True(task.HasExited);
            Assert.Contains("SDK cancel callback failed", task.LastError);
            Assert.True(task.ConsecutiveFailures > 0);
            await task.StopAsync(1000);
        }
        finally { task.Dispose(); }
    }

    [Fact]
    public async Task ActualStopReportsCallbackFailureButReleasesOnlyExitedTask()
    {
        var reading = Signal();
        var task = new OpcUaDataAcqTask("callback-fault", async ct =>
        {
            using var registered = ct.Register(() => throw new InvalidOperationException("SDK stop callback failed"));
            reading.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return new();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
        var plugin = new OpcUaDataAcqStopPlugin();
        var setting = new OpcUaDataAcqStopSetting { TaskName = "\"callback-fault\"", TimeoutMs = 1000 };
        var (context, state) = AcquisitionContext.Create(plugin, setting);
        const string key = "OpcUaDataAcq_callback-fault"; state.Resources[key] = task;
        try
        {
            await reading.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await ExpressionEvaluatorFactory.Default.EvalStringAsync(setting.TaskName, context);
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.Contains("SDK stop callback failed", task.CancellationError!.ToString());
            Assert.False(state.Resources.ContainsKey(key));
            Assert.True(task.HasExited);
        }
        finally { await task.StopAsync(1000); task.Dispose(); }
    }

    [Fact]
    public async Task DisconnectRetainsNaturallyCompletedAcquisitionUntilActualConsumerExits()
    {
        int closed = 0;
        var connection = new OpcUaConnectionResource(() => { Interlocked.Increment(ref closed); return Task.CompletedTask; }, 1000);
        var task = connection.StartAcquisition("finite", [new() { NodeId = "ns=2;s=V" }], 1, 30, 100);
        var entered = Signal(); var release = Signal(); Task<int>? consuming = null;
        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!task.HasExited) { Assert.True(clock.ElapsedMilliseconds < 2000); await Task.Delay(5); }
            consuming = task.ConsumeAsync(-1, async _ => { entered.SetResult(); await release.Task; return 1; }, default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<TimeoutException>(() => connection.CloseAsync(30));
            Assert.Equal(0, closed); Assert.True(task.IsStopRequested);
            await Assert.ThrowsAsync<InvalidOperationException>(() => task.ConsumeAsync(-1, _ => Task.FromResult(0), default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => consuming.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(connection.IsClosed);
            release.SetResult(); await Assert.ThrowsAsync<InvalidOperationException>(() => consuming);
            await connection.CloseAsync(1000);
            Assert.True(connection.IsClosed); Assert.Equal(1, closed);
        }
        finally
        {
            release.TrySetResult(); if (consuming != null) await Assert.ThrowsAsync<InvalidOperationException>(() => consuming);
            task.Dispose(); await connection.CloseAsync(1000);
        }
    }

    [Fact]
    public async Task ExplicitAcquisitionCloseRemovesCollectorsAndDoesNotRepeatConnectionCleanup()
    {
        int closed = 0;
        var connection = new OpcUaConnectionResource(() => { Interlocked.Increment(ref closed); return Task.CompletedTask; }, 1000);
        try
        {
            for (int i = 0; i < 8; i++)
            {
                var task = connection.StartAcquisition("cycle-" + i, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
                await task.StopAsync(1000); task.Dispose(); task.Dispose();
                await task.CloseCompletion.WaitAsync(TimeSpan.FromSeconds(2));
            }
            var state = typeof(OpcUaConnectionResource).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(connection)!;
            var collectors = (HashSet<OpcUaDataAcqTask>)typeof(OpcUaConnectionResource)
                .GetField("_collectors", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(connection)!;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                lock (state) if (collectors.Count == 0) break;
                Assert.True(clock.ElapsedMilliseconds < 1000); await Task.Delay(5);
            }
            await connection.CloseAsync(1000); await connection.CloseAsync(1000);
            Assert.Equal(1, closed);
        }
        finally { await connection.CloseAsync(1000); }
    }

    [Fact]
    public void ReadSettingExposesCompatibleFiniteTotalBudget()
    {
        var property = typeof(OpcUaDataAcqReadSetting).GetProperty("TimeoutMs");
        Assert.NotNull(property);
        var restored = new OpcUaDataAcqReadPlugin().CreateSerializer().Deserialize([0x80], 1);
        Assert.Equal(5000, property.GetValue(restored));
        Assert.Contains("| TimeoutMs |", new OpcUaDataAcqReadPlugin().Description);
    }

    [Fact]
    public async Task QueueDeadlineDoesNotStartAnotherConsumerAfterBudgetExpires()
    {
        var (task, ready) = CreateOneRecord();
        var entered = Signal(); var release = Signal();
        Task<int>? first = null;
        int submitted = 0;
        try
        {
            await ready.WaitAsync(TimeSpan.FromSeconds(3));
            first = task.ConsumeAsync(-1, async _ => { entered.SetResult(); await release.Task; return 1; }, default);
            await entered.Task;
            await Assert.ThrowsAsync<TimeoutException>(() => task.ConsumeWithBudgetAsync(-1,
                (_, _) => { Interlocked.Increment(ref submitted); return Task.FromResult(1); }, 20, default));
            release.SetResult(); await first;
            await task.StopAsync(1000);
            Assert.Equal(0, submitted);
        }
        finally { release.TrySetResult(); if (first != null) await first; await task.StopAsync(1000); task.Dispose(); }
    }

    [Fact]
    public async Task SuccessfulLateExternalCommitIsNotExportedAgainButNeverPublishes()
    {
        var (task, ready) = CreateOneRecord();
        var entered = Signal(); var release = Signal();
        int published = 0;
        try
        {
            await ready.WaitAsync(TimeSpan.FromSeconds(3));
            var consuming = task.ConsumeWithBudgetAsync(-1, async (records, operation) =>
            {
                entered.SetResult(); await release.Task;
                operation.MarkExternalOutputCommitted(); return records.Count;
            }, 30, default, _ => Interlocked.Increment(ref published));
            await entered.Task;
            await Assert.ThrowsAsync<TimeoutException>(() => consuming);
            Assert.Equal(1, task.SampleCount);
            await Assert.ThrowsAsync<TimeoutException>(() => task.StopAsync(20));
            Assert.False(task.HasExited);
            release.SetResult();
            await task.Completion.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(0, task.SampleCount);
            Assert.Equal(1, task.LastExportCommitSequence);
            Assert.Equal(0, published);
        }
        finally { release.TrySetResult(); await task.StopAsync(1000); task.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualReadBudgetEndsBeforeExportAndCannotPublishIntoReplacement(bool cancel)
    {
        var (task, ready) = CreateOneRecord();
        var entered = Signal(); var release = Signal();
        task.CsvExporter = async (_, _, _) => { entered.TrySetResult(); await release.Task; };
        var plugin = new OpcUaDataAcqReadPlugin();
        var setting = new OpcUaDataAcqReadSetting { TaskName = "\"budget\"", TimeoutMs = cancel ? 1000 : 40,
            ResultVariable = "Locals.Wave", RecordsJsonVariable = "Locals.Records", SaveToFile = true, CsvFilePath = "\"managed.csv\"" };
        var (context, state) = AcquisitionContext.Create(plugin, setting);
        const string key = "OpcUaDataAcq_budget";
        state.Resources[key] = task;
        using var caller = new CancellationTokenSource();
        try
        {
            await ready.WaitAsync(TimeSpan.FromSeconds(3));
            // 先加载 SDK 脚本缓存，测试只量化消费/导出期限，不把首次 Roslyn 初始化当 I/O。
            await ExpressionEvaluatorFactory.Default.EvalStringAsync(setting.TaskName, context);
            await ExpressionEvaluatorFactory.Default.EvalStringAsync(setting.CsvFilePath, context);
            var reading = plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            if (cancel) caller.Cancel();
            var result = await reading.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(cancel ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            if (!cancel) { Assert.Contains("40", result.StepResult.Error!.Message); Assert.Contains("Timeout", result.StepResult.Error.Detail); }
            Assert.Empty(state.Published);
            Assert.Equal(1, task.SampleCount);
            state.Resources[key] = new object(); // 模拟原执行结束/同名下一代对象，旧后台不得写入任何变量。
            task.RequestStop(); release.SetResult();
            await task.Completion.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Empty(state.Published);
            Assert.Equal(0, task.SampleCount);
            Assert.Equal(1, task.LastExportCommitSequence);
        }
        finally { release.TrySetResult(); await task.StopAsync(1000); task.Dispose(); }
    }

    [Fact]
    public async Task PublicationFailureWithoutExternalCommitRetainsFifo()
    {
        var (task, ready) = CreateOneRecord();
        try
        {
            await ready.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<InvalidOperationException>(() => task.ConsumeWithBudgetAsync(-1,
                (records, _) => Task.FromResult(records.Count), 1000, default, _ => throw new InvalidOperationException("host variable failure")));
            Assert.Equal(1, task.SampleCount);
        }
        finally { await task.StopAsync(1000); task.Dispose(); }
    }

    [Fact]
    public async Task ConfirmedExternalCommitSurvivesSubsequentPreparationFailure()
    {
        var (task, ready) = CreateOneRecord();
        try
        {
            await ready.WaitAsync(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAsync<InvalidOperationException>(() => task.ConsumeWithBudgetAsync<int>(-1,
                (_, operation) => { operation.MarkExternalOutputCommitted(); throw new InvalidOperationException("failure after CSV commit"); }, 1000, default));
            Assert.Equal(0, task.SampleCount);
            Assert.Equal(1, task.LastExportCommitSequence);
            Assert.Contains("failure after CSV commit", task.LastConsumptionError);
        }
        finally { await task.StopAsync(1000); task.Dispose(); }
    }

    [Fact]
    public async Task InvalidReadBudgetIsRejectedByActualEditorAndExecutor()
    {
        var plugin = new OpcUaDataAcqReadPlugin();
        var setting = new OpcUaDataAcqReadSetting { TimeoutMs = 0 };
        var (context, _) = AcquisitionContext.Create(plugin, setting);
        var step = context.CurrentStep!.Step;
        var errors = await new OpcUa.UI.OpcUaDataAcqReadEditorPlugin().ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = step.StepSetting.Setting, Evaluator = ExpressionEvaluatorFactory.Default,
            ExecutionContext = context, SequenceFile = new SequenceFile(), Block = [], CurrentStep = step
        });
        Assert.Contains(errors, error => error.Message.Contains("TimeoutMs"));
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("总期限", result.StepResult.Error!.Message);
    }

    [Fact]
    public void ActualTimeoutControlPersistsBeforeRefreshAndUnload() => UiThread.Run(() =>
    {
        var serializer = new OpcUaDataAcqReadPlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault());
        var page = new OpcUa.UI.Views.OpcUaDataAcqReadEditorView(); page.RefreshFromStep(step);
        page.Measure(new Size(1000, 700)); page.Arrange(new Rect(0, 0, 1000, 700)); page.UpdateLayout();
        var box = Assert.IsType<TextBox>(page.FindName("ConsumptionTimeoutBox"));
        Assert.Equal("5000", box.Text);
        box.Text = "137"; BindingOperations.GetBindingExpression(box, TextBox.TextProperty)!.UpdateSource();
        page.RefreshFromStep(step);
        Assert.Equal(137, page.ViewModel.TimeoutMs);
        box.Text = "211"; BindingOperations.GetBindingExpression(box, TextBox.TextProperty)!.UpdateSource();
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal(211, ((OpcUaDataAcqReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)).TimeoutMs);
    });

    internal static (OpcUaDataAcqTask Task, Task Ready) CreateOneRecord()
    {
        int calls = 0;
        var ready = Signal();
        var task = new OpcUaDataAcqTask("budget", async ct =>
        {
            if (Interlocked.Increment(ref calls) == 1) return new() { new(new Variant(5)) };
            ready.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return new();
        }, [new() { NodeId = "ns=2;s=V" }], 1, 0, 100);
        return (task, ready.Task);
    }

    internal static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public class AcquisitionContext : DispatchProxy
{
    public ConcurrentDictionary<string, object> Resources = new();
    public ConcurrentQueue<(string Path, object? Value)> Published = new();
    private object _registry = null!;
    private StepExecutionInfo _step = null!;
    public static (IExecutionContext Context, AcquisitionContext State) Create(IStepPlugin plugin, object setting)
    {
        var context = DispatchProxy.Create<IExecutionContext, AcquisitionContext>();
        var state = (AcquisitionContext)(object)context;
        var step = new Step(); step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion;
        step.StepSetting.Setting = plugin.CreateSerializer().Serialize(setting);
        state._step = new() { Step = step };
        var type = typeof(IExecutionContext).GetProperty("Resources")?.PropertyType ??
            typeof(IExecutionContext).GetInterfaces().SelectMany(i => i.GetProperties()).First(p => p.Name == "Resources").PropertyType;
        state._registry = DispatchProxy.Create(type, typeof(RaceStore));
        ((RaceStore)state._registry).Values = state.Resources;
        return (context, state);
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "get_CurrentStep") return _step;
        if (method.Name == "get_Resources") return _registry;
        if (method.Name == "SetVariable") { Published.Enqueue(((string)args![0]!, args[1])); return null; }
        if (method.Name == "get_IsAbortRequested") return false;
        return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
