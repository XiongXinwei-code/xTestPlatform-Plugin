using System.Reflection;
using System.Runtime.ExceptionServices;
using SerialPort.Helpers;
using SerialPort.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace SerialPort.Tests;

[Collection("Serial followup isolated")]
public sealed class OutputPreflightBoundaryTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (bool query in new[] { false, true })
        foreach (bool atGet in new[] { false, true })
        foreach (string transition in new[] { "cancel", "CleanupExecution", "CleanupAll" })
            yield return new object[] { query, atGet, transition };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Declaration_lookup_cannot_hide_original_cancellation_or_the_retired_sdk_scope(bool query, bool atGet, string transition)
    {
        IStepPlugin plugin = query ? new SerialPortQueryPlugin() : new SerialPortReadPlugin();
        object setting = query
            ? new SerialPortQuerySetting { WriteData = "\"request\"", ReadBytes = 1, ResultVariable = "Locals.rx" }
            : new SerialPortReadSetting { ReadBytes = 1, ResultVariable = "Locals.rx" };
        var (inner, state) = PublicationContextProxy.Create(plugin, setting);
        var actual = SdkRegistryForwarder.NewActual();
        state.ResourcesOverride = actual;
        var channel = new Channel();
        var owner = new SerialConnectionResource(channel);
        Assert.True(actual.TryAdd(SerialPortHelper.GetPortKey("COM1"), owner, ResourceLifetime.Execution));
        var context = PreflightLookupContext.Wrap(inner, out var variables);
        variables.Found = atGet; variables.Value = 123;
        using var caller = new CancellationTokenSource();
        int entered = 0;
        variables.BeforeLookup = method =>
        {
            if (method != (atGet ? "GetVariable" : "HasVariable")) return;
            entered++;
            if (transition == "cancel") caller.Cancel();
            else SdkRegistryForwarder.Cleanup(actual, transition == "CleanupAll");
        };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.Equal(1, entered);
            Assert.Equal(transition == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            if (transition != "cancel") Assert.Contains("资源作用域已回收", result.StepResult.Error!.Message + result.StepResult.Error.Detail);
            Assert.Equal(0, channel.Reads); Assert.Equal(0, channel.Writes); Assert.Empty(state.Published);
            variables.BeforeLookup = null;
            var next = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, next.StepResult.Status);
            Assert.DoesNotContain("资源作用域已回收", next.StepResult.Error!.Message + next.StepResult.Error.Detail);
        }
        finally { variables.BeforeLookup = null; await owner.CloseAsync(2000, default); SdkRegistryForwarder.Cleanup(actual, true); }
    }

    private sealed class Channel : ISerialChannel
    {
        internal int Reads, Writes;
        public bool IsOpen { get; private set; } = true;
        public int ReadTimeout { get; set; } = 1000;
        public int WriteTimeout { get; set; } = 1000;
        public int Read(byte[] buffer, int offset, int count) { Interlocked.Increment(ref Reads); buffer[offset] = (byte)'X'; return 1; }
        public void Write(byte[] bytes) => Interlocked.Increment(ref Writes);
        public void Dispose() => IsOpen = false;
    }
}

public class PreflightLookupContext : DispatchProxy
{
    private IExecutionContext _inner = null!;
    internal Action<string>? BeforeLookup;
    internal bool Found;
    internal object? Value;
    internal static IExecutionContext Wrap(IExecutionContext inner, out PreflightLookupContext variables)
    {
        var context = Create<IExecutionContext, PreflightLookupContext>();
        variables = (PreflightLookupContext)(object)context; variables._inner = inner;
        return context;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name is "HasVariable" or "GetVariable")
        {
            BeforeLookup?.Invoke(method.Name);
            return method.Name == "HasVariable" ? Found : Value;
        }
        try { return method.Invoke(_inner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
}
