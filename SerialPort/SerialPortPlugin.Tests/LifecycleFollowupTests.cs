using System.Reflection;
using System.Runtime.ExceptionServices;
using SerialPort.Executors;
using SerialPort.Helpers;
using SerialPort.Models;
using SerialPort.UI;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace SerialPort.Tests;

[CollectionDefinition("Serial followup isolated", DisableParallelization = true)]
public sealed class SerialFollowupCollection { }

[Collection("Serial followup isolated")]
public sealed class LifecycleFollowupTests
{
    private static int _port = 710000;

    [Theory]
    [InlineData("factory")]
    [InlineData("log")]
    public async Task Open_keeps_the_configured_total_budget_through_factory_and_final_log(string stage)
    {
        string name = "COM" + Interlocked.Increment(ref _port);
        var setting = new SerialPortOpenSetting { PortName = $"\"{name}\"", TimeoutMs = 500 };
        var (context, state) = PublicationContextProxy.Create(new SerialPortOpenPlugin(), setting);
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>(setting.PortName, context); // exclude cold Roslyn setup from the probe.
        int factoryCalls = 0, opens = 0; bool held = false;
        SerialConnectionResource? first = null;
        var executor = new SerialPortOpenExecutor((_, _, _) =>
        {
            int call = Interlocked.Increment(ref factoryCalls);
            if (stage == "factory" && call == 1) { held = true; Thread.Sleep(650); }
            var owner = new SerialConnectionResource(new Channel());
            if (call == 1) first = owner;
            return (owner, () => Interlocked.Increment(ref opens));
        });
        if (stage == "log") state.OnLog = () => { held = true; Thread.Sleep(650); };
        try
        {
            var failed = await executor.ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(held, "The controlled factory/log boundary was not reached.");
            Assert.Equal(TestStatus.Error, failed.StepResult.Status);
            Assert.Contains("500", failed.StepResult.Error!.Message + failed.StepResult.Error.Detail);
            Assert.Equal(stage == "factory" ? 0 : 1, opens);
            await first!.CloseAsync(2000, default);
            state.OnLog = null;
            var retry = await executor.ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(TestStatus.Completed, retry.StepResult.Status);
            Assert.Equal(stage == "factory" ? 1 : 2, opens);
        }
        finally
        {
            if (first != null) await first.CloseAsync(2000, default);
            if (state.Store.Values.TryGetValue(SerialPortHelper.GetPortKey(name), out var owner))
                await ((SerialConnectionResource)owner).CloseAsync(2000, default);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Close_log_deadline_failure_keeps_the_closed_fact_and_never_closes_a_new_owner(bool existing)
    {
        string name = "COM" + Interlocked.Increment(ref _port), key = SerialPortHelper.GetPortKey(name);
        var setting = new SerialPortCloseSetting { PortName = $"\"{name}\"", TimeoutMs = 500 };
        var (context, state) = PublicationContextProxy.Create(new SerialPortClosePlugin(), setting);
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>(setting.PortName, context);
        var old = new SerialConnectionResource(new Channel());
        var replacement = new SerialConnectionResource(new Channel());
        if (existing) state.Store.Values[key] = old;
        bool held = false;
        state.OnLog = () => { held = true; state.Store.Values[key] = replacement; Thread.Sleep(650); };
        try
        {
            var result = await new SerialPortCloseExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(held);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("500", result.StepResult.Error!.Message + result.StepResult.Error.Detail);
            if (existing) Assert.True(old.IsClosed);
            Assert.Same(replacement, state.Store.Values[key]); Assert.False(replacement.IsClosed);
        }
        finally { await old.CloseAsync(2000, default); await replacement.CloseAsync(2000, default); }
    }

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(true, "missing")]
    [InlineData(false, "wrong")]
    [InlineData(true, "wrong")]
    [InlineData(false, "unscoped")]
    [InlineData(true, "unscoped")]
    public async Task Invalid_output_is_rejected_before_reading_or_sending_query(bool query, string invalid)
    {
        string path = invalid == "unscoped" ? "rx" : "Locals.rx";
        var (plugin, setting) = Reading(query, path);
        var (inner, state) = PublicationContextProxy.Create(plugin, setting);
        var context = FollowupVariableContext.Wrap(inner, out var variables);
        if (invalid != "missing") variables.Declared[path] = invalid == "wrong" ? 123 : "";
        var channel = new Channel(); var owner = new SerialConnectionResource(channel);
        state.Store.Values[SerialPortHelper.GetPortKey("COM1")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, channel.Reads); Assert.Equal(0, channel.Writes); Assert.Empty(state.Published);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Theory]
    [InlineData(false, "empty")]
    [InlineData(true, "empty")]
    [InlineData(false, "null")]
    [InlineData(true, "null")]
    [InlineData(false, "text")]
    [InlineData(true, "text")]
    public async Task Empty_output_or_a_declared_string_target_keeps_compatible_read_behavior(bool query, string value)
    {
        string path = value == "empty" ? "" : "Locals.rx";
        var (plugin, setting) = Reading(query, path);
        var (inner, state) = PublicationContextProxy.Create(plugin, setting);
        var context = FollowupVariableContext.Wrap(inner, out var variables);
        if (path.Length != 0) variables.Declared[path] = value == "null" ? null : "";
        var channel = new Channel(); var owner = new SerialConnectionResource(channel);
        state.Store.Values[SerialPortHelper.GetPortKey("COM1")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.Equal("X", result.StepResult.Value); Assert.Equal(1, channel.Reads);
            Assert.Equal(query ? 1 : 0, channel.Writes);
            Assert.Equal(path.Length == 0 ? 0 : 1, state.Published.Count);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("query")]
    public async Task Cancellation_inside_the_real_sdk_missing_resource_lookup_returns_aborted(string step)
    {
        IStepPlugin plugin = step switch { "read" => new SerialPortReadPlugin(), "write" => new SerialPortWritePlugin(), _ => new SerialPortQueryPlugin() };
        object setting = step switch { "read" => new SerialPortReadSetting(), "write" => new SerialPortWriteSetting { WriteData = "\"request\"" }, _ => new SerialPortQuerySetting { WriteData = "\"request\"" } };
        var (context, state) = PublicationContextProxy.Create(plugin, setting);
        var actual = SdkRegistryForwarder.NewActual();
        state.ResourcesOverride = SdkRegistryForwarder.Wrap(actual, out var routing);
        using var caller = new CancellationTokenSource();
        routing.OnTryGet = (key, type) => { if (key == SerialPortHelper.GetPortKey("COM1") && type == typeof(SerialConnectionResource)) caller.Cancel(); };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(caller.IsCancellationRequested); Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            Assert.Empty(state.Published);
        }
        finally { SdkRegistryForwarder.Cleanup(actual, true); }
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, "rx")]
    [InlineData(true, "rx")]
    public async Task Editor_validation_matches_optional_output_and_full_path_contract(bool query, string path)
    {
        var (plugin, setting) = Reading(query, path);
        var (inner, _) = PublicationContextProxy.Create(plugin, setting);
        var context = FollowupVariableContext.Wrap(inner, out var variables);
        if (path.Length != 0) variables.Declared[path] = "";
        IStepEditorPlugin editor = query ? new SerialPortQueryEditorPlugin() : new SerialPortReadEditorPlugin();
        var current = context.CurrentStep!.Step;
        var errors = await editor.ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = current.StepSetting.Setting, CurrentStep = current, Block = [current],
            SequenceFile = new SequenceFile(), ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default
        });
        if (path.Length == 0) Assert.DoesNotContain(errors, e => e.Code is "SP_033" or "SP_042");
        else Assert.Contains(errors, e => e.Message.Contains("作用域", StringComparison.Ordinal));
    }

    private static (IStepPlugin Plugin, object Setting) Reading(bool query, string path) => query
        ? (new SerialPortQueryPlugin(), new SerialPortQuerySetting { WriteData = "\"request\"", ReadBytes = 1, Terminator = "", ResultVariable = path })
        : (new SerialPortReadPlugin(), new SerialPortReadSetting { ReadBytes = 1, Terminator = "", ResultVariable = path });

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

public class FollowupVariableContext : DispatchProxy
{
    internal readonly Dictionary<string, object?> Declared = new();
    private IExecutionContext _inner = null!;
    internal static IExecutionContext Wrap(IExecutionContext inner, out FollowupVariableContext variables)
    {
        var result = Create<IExecutionContext, FollowupVariableContext>();
        variables = (FollowupVariableContext)(object)result; variables._inner = inner;
        return result;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "HasVariable") return Declared.ContainsKey((string)args![0]!);
        if (method.Name == "GetVariable") return Declared.GetValueOrDefault((string)args![0]!);
        try { return method.Invoke(_inner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
}
