using System.Reflection;
using System.Runtime.ExceptionServices;
using SerialPort.Executors;
using SerialPort.Helpers;
using SerialPort.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace SerialPort.Tests;

public sealed class ExecutionEpochTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_write_with_pending_name_expression_cannot_use_owner_from_next_sdk_execution(bool cleanupAll)
    {
        using var guard = new CancellationTokenSource(15000);
        var actual = SdkRegistryForwarder.NewActual();
        var name = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new SerialPortWriteExecutor((expression, _) =>
        {
            if (expression == "\"C\"") { entered.TrySetResult(); return name.Task; }
            return Task.FromResult(expression.Trim('"'));
        });
        var (context, state) = PublicationContextProxy.Create(new SerialPortWritePlugin(), new SerialPortWriteSetting { PortName = "\"C\"", WriteData = "\"payload\"" });
        state.ResourcesOverride = actual;
        var execution = executor.ExecuteAsync(context, guard.Token);
        await entered.Task.WaitAsync(guard.Token);
        SdkRegistryForwarder.Cleanup(actual, cleanupAll);
        int writes = 0;
        var fresh = new SerialConnectionResource(new Channel(() => { }, () => Interlocked.Increment(ref writes)));
        string key = SerialPortHelper.GetPortKey("C"); actual.Set(key, fresh, ResourceLifetime.Execution);
        try
        {
            name.SetResult("C");
            Assert.Equal(TestStatus.Error, (await execution.WaitAsync(guard.Token)).StepResult.Status);
            Assert.False(guard.IsCancellationRequested); Assert.Equal(0, writes);
            Assert.True(actual.TryGet<SerialConnectionResource>(key, out var current)); Assert.Same(fresh, current); Assert.False(fresh.IsClosed);
        }
        finally { name.TrySetResult("C"); await execution.WaitAsync(guard.Token); await fresh.CloseAsync(1000, default); SdkRegistryForwarder.Cleanup(actual, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sdk_cleanup_freezes_old_Open_waiting_for_previous_owner_and_first_new_execution_can_open(bool cleanupAll)
    {
        using var guard = new CancellationTokenSource(15000);
        using var release = new ManualResetEventSlim();
        var disposing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actual = SdkRegistryForwarder.NewActual();
        var registry = SdkRegistryForwarder.Wrap(actual, out var routing);
        string name = "COM" + Random.Shared.Next(30000, 50000), key = SerialPortHelper.GetPortKey(name);
        var old = new SerialConnectionResource(new Channel(() => { disposing.TrySetResult(); release.Wait(guard.Token); }));
        actual.Set(key, old, ResourceLifetime.Execution);
        old.Dispose(); await disposing.Task.WaitAsync(guard.Token);
        routing.OnTryGet = (port, type) => { if (port == key && type == typeof(object)) captured.TrySetResult(); };
        int opens = 0;
        var executor = new SerialPortOpenExecutor((port, _, _) => (new SerialConnectionResource(new Channel(() => { }), physicalPort: port), () => Interlocked.Increment(ref opens)));
        var (context, state) = PublicationContextProxy.Create(new SerialPortOpenPlugin(), new SerialPortOpenSetting { PortName = "\"" + name + "\"", TimeoutMs = 4000 });
        state.ResourcesOverride = registry;
        var scheduler = new HeldContinuationContext();
        var opening = scheduler.Start(() => executor.ExecuteAsync(context, guard.Token));
        try
        {
            await scheduler.PumpUntilAsync(captured.Task, opening, guard.Token);
            Assert.False(opening.IsCompleted); Assert.Equal(0, opens);
            SdkRegistryForwarder.Cleanup(actual, cleanupAll); // SDK lifecycle, not caller cancellation.
            release.Set(); await old.CloseAsync(2000, guard.Token);
            await scheduler.DrainUntilAsync(opening, guard.Token);
            Assert.False(guard.IsCancellationRequested);
            Assert.Equal(TestStatus.Error, (await opening).StepResult.Status);
            Assert.Equal(0, opens);
            Assert.False(actual.TryGet<SerialConnectionResource>(key, out _));

            Assert.Equal(TestStatus.Completed, (await executor.ExecuteAsync(context, guard.Token).WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(1, opens);
            Assert.True(actual.TryGet<SerialConnectionResource>(key, out var current));
            Assert.NotSame(old, current); Assert.False(current.IsClosed);
        }
        finally
        {
            release.Set(); await scheduler.DrainUntilAsync(opening, guard.Token);
            if (actual.TryGet<SerialConnectionResource>(key, out var owner)) await owner.CloseAsync(2000, default);
            SdkRegistryForwarder.Cleanup(actual, true);
        }
    }

    private sealed class Channel(Action release, Action? write = null) : ISerialChannel
    {
        public bool IsOpen => true;
        public int ReadTimeout { get; set; } = 1000;
        public int WriteTimeout { get; set; } = 1000;
        public int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public void Write(byte[] bytes) => write?.Invoke();
        public void Dispose() => release();
    }
}

public class SdkRegistryForwarder : DispatchProxy
{
    private IResourceRegistry _actual = null!;
    internal Action<string, Type>? OnTryGet;
    internal static IResourceRegistry NewActual() => (IResourceRegistry)Activator.CreateInstance(
        typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
    internal static IResourceRegistry Wrap(IResourceRegistry actual, out SdkRegistryForwarder routing)
    {
        var registry = Create<IResourceRegistry, SdkRegistryForwarder>(); routing = (SdkRegistryForwarder)(object)registry; routing._actual = actual; return registry;
    }
    internal static void Cleanup(IResourceRegistry actual, bool all) => actual.GetType()
        .GetMethod(all ? "CleanupAll" : "CleanupExecution", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null);
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "TryGet") OnTryGet?.Invoke((string)args![0]!, method.GetGenericArguments()[0]);
        try { return method.Invoke(_actual, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null) { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
}
