using System.Collections;
using System.Text.Json;
using AuditTestSupport;
using NiDaq.Executors;
using NiDaq.Helpers;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class ExpansionBehaviorTests
{
    private static async Task<DaqTaskResource> Owner(DaqTaskKind kind, string[]? channels = null, int[]? widths = null, bool buffered = false, bool started = true)
    {
        var owner = new DaqTaskResource(() => { }, () => { });
        await owner.ConfigureAsync(() => { owner.SetKind(kind); if (channels != null) owner.SetDigitalLayout(channels, widths!, buffered); }, default);
        typeof(DaqTaskResource).GetProperty(nameof(DaqTaskResource.Started))!.SetValue(owner, started);
        return owner;
    }
    private static (IExecutionContext Context, ContextProxy State) Context(IStepPlugin plugin, object setting, DaqTaskResource owner)
    {
        var pair = ContextProxy.Create(plugin, setting); ((StoreProxy)pair.State.Store!).Values["NiDaq.Task:T"] = owner; return pair;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Counter_and_encoder_readers_reject_each_others_task_kind(bool encoderReader)
    {
        var owner = await Owner(encoderReader ? DaqTaskKind.CounterEdges : DaqTaskKind.Encoder);
        IStepPlugin plugin = encoderReader ? new NiDaqEncoderReadPlugin() : new NiDaqCounterReadPlugin();
        object setting = encoderReader ? new NiDaqEncoderSetting { TaskName = "\"T\"", ResultVariable = "Locals.r" }
            : new NiDaqCounterReadSetting { TaskName = "\"T\"", ResultVariable = "Locals.r" };
        var (context, state) = Context(plugin, setting, owner); var backend = new FakeBackend();
        try
        {
            var result = encoderReader ? await new NiDaqEncoderReadExecutor().ExecuteAsync(context) : await new DaqExpansionExecutor(plugin, backend).ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Contains("任务类型不匹配", result.StepResult.Error!.Message);
            Assert.Empty(state.Published); Assert.Equal(0, backend.Reads);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Counter_read_preserves_uint_count_or_double_frequency(bool frequency)
    {
        var owner = await Owner(frequency ? DaqTaskKind.CounterFrequency : DaqTaskKind.CounterEdges);
        var plugin = new NiDaqCounterReadPlugin(); var (context, state) = Context(plugin, new NiDaqCounterReadSetting { TaskName = "\"T\"", ResultVariable = "Locals.r" }, owner);
        var backend = new FakeBackend { CounterValue = frequency ? 120.5 : (object)uint.MaxValue };
        try
        {
            Assert.Equal(TestStatus.Completed, (await new DaqExpansionExecutor(plugin, backend).ExecuteAsync(context)).StepResult.Status);
            var value = Assert.Single(state.Published).Value;
            if (frequency) Assert.Equal(120.5, Assert.IsType<double>(value)); else Assert.Equal(uint.MaxValue, Assert.IsType<uint>(value));
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Digital_read_exports_channel_major_layout_and_rejects_incomplete_samples(bool incomplete)
    {
        var owner = await Owner(DaqTaskKind.DigitalInput, ["Fake/port0", "Fake/port1"], [8, 8], true);
        var plugin = new NiDaqDigitalReadPlugin(); var (context, state) = Context(plugin, new NiDaqDigitalReadSetting { TaskName = "\"T\"", SamplesPerChannel = 2, ResultVariable = "Locals.r" }, owner);
        var backend = new FakeBackend { DigitalValues = incomplete ? new uint[,] { { 1 }, { 3 } } : new uint[,] { { 1, 2 }, { 3, 4 } } };
        try
        {
            var result = await new DaqExpansionExecutor(plugin, backend).ExecuteAsync(context);
            Assert.Equal(incomplete ? TestStatus.Error : TestStatus.Completed, result.StepResult.Status);
            if (incomplete) Assert.Empty(state.Published);
            else
            {
                using var json = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(state.Published).Value));
                Assert.Equal("Fake/port1", json.RootElement.GetProperty("channels")[1].GetString());
                Assert.Equal(2, json.RootElement.GetProperty("samplesPerChannel").GetInt32());
                Assert.Equal(3u, json.RootElement.GetProperty("data")[1][0].GetUInt32());
            }
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Digital_read_postprocessing_honors_total_deadline_and_cancellation_without_publish(bool cancel)
    {
        var owner = await Owner(DaqTaskKind.DigitalInput, ["Fake/port0"], [8], true);
        var plugin = new NiDaqDigitalReadPlugin(); var (context, state) = Context(plugin, new NiDaqDigitalReadSetting { TaskName = "\"T\"", SamplesPerChannel = 2048, ResultVariable = "Locals.r", TimeoutMs = 100 }, owner);
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>("\"T\"", context);
        using var caller = new CancellationTokenSource(); int readReturned = 0, postChecks = 0;
        var backend = new FakeBackend { DigitalValues = new uint[1, 2048], OnRead = () => Volatile.Write(ref readReturned, 1) };
        owner.ExecutionCheck = () =>
        {
            if (Volatile.Read(ref readReturned) == 1 && Interlocked.Increment(ref postChecks) == 2)
            { if (cancel) caller.Cancel(); else Thread.Sleep(150); }
        };
        try
        {
            Assert.Equal(cancel ? TestStatus.Aborted : TestStatus.Error, (await new DaqExpansionExecutor(plugin, backend).ExecuteAsync(context, caller.Token)).StepResult.Status);
            Assert.Equal(1, backend.Reads); Assert.True(postChecks >= 2); Assert.Empty(state.Published);
        }
        finally { owner.ExecutionCheck = null; await owner.CloseAsync(2000, default); Assert.Empty(state.Published); }
    }

    [Theory]
    [InlineData("Fake/port0/line4:7", 4, "0xF0u", true)]
    [InlineData("Fake/port0/line4:7", 4, "1u", false)]
    [InlineData("Fake/port0/line7", 1, "0x80u", true)]
    [InlineData("Fake/port0/line31", 1, "0x80000000u", true)]
    [InlineData("Fake/port0", 8, "255u", true)]
    [InlineData("Fake/port0", 8, "256u", false)]
    public async Task Digital_write_preserves_physical_line_bits(string channel, int width, string expression, bool valid)
    {
        var owner = await Owner(DaqTaskKind.DigitalOutput, [channel], [width]); var plugin = new NiDaqDigitalWritePlugin();
        var (context, _) = Context(plugin, new NiDaqDigitalWriteSetting { TaskName = "\"T\"", Values = expression }, owner); var backend = new FakeBackend();
        try
        {
            var result = await new DaqExpansionExecutor(plugin, backend).ExecuteAsync(context);
            Assert.Equal(valid ? TestStatus.Completed : TestStatus.Error, result.StepResult.Status); Assert.Equal(valid ? 1 : 0, backend.Writes);
            if (valid) Assert.Equal(await ExpressionEvaluatorFactory.Default.EvaluateAsync<uint>(expression, context), backend.LastWrite![0, 0]);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Fact]
    public void Digital_conversion_rejects_deferred_enumeration_and_invalid_shapes()
    {
        var deferred = new MustNeverEnumerate();
        Assert.Throws<ArgumentException>(() => DaqExpansionValidation.DigitalValues(deferred, 1)); Assert.False(deferred.Entered);
        Assert.Throws<ArgumentException>(() => DaqExpansionValidation.DigitalValues(new object(), 1));
        Assert.Throws<ArgumentException>(() => DaqExpansionValidation.DigitalValues(new[] { new[] { 1, 2 }, new[] { 3 } }, 2));
        Assert.Throws<ArgumentException>(() => DaqExpansionValidation.DigitalValues(-1, 1));
        Assert.Throws<ArgumentException>(() => DaqExpansionValidation.DigitalValues(1.2, 1));
        Assert.Throws<ArgumentException>(() => DaqExpansionValidation.DigitalValues(4294967294.9999995d, 1));
        Assert.Throws<ArgumentException>(() => DaqExpansionValidation.DigitalValues(255.99998f, 1));
        Assert.Equal(16777216u, DaqExpansionValidation.DigitalValues(16777216f, 1)[0, 0]);
        Assert.Equal(4294967040u, DaqExpansionValidation.DigitalValues(4294967040f, 1)[0, 0]);
        Assert.Throws<ArgumentException>(() => DaqExpansionValidation.DigitalChannels("Fake/port0/line32"));
        Assert.Throws<ArgumentException>(() => DaqExpansionValidation.DigitalChannels("Fake/port0/line7:4"));
        var matrix = DaqExpansionValidation.DigitalValues(new List<List<uint>> { new() { 1, 2 }, new() { 3, 4 } }, 2);
        Assert.Equal(3u, matrix[1, 0]);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => DaqExpansionValidation.DigitalValues(new[] { 1, 2 }, 1, cancelled.Token.ThrowIfCancellationRequested));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Digital_write_buffer_preload_does_not_start_task_and_ondemand_requires_start(bool buffered)
    {
        var owner = await Owner(DaqTaskKind.DigitalOutput, ["Fake/port0"], [8], buffered, false); var plugin = new NiDaqDigitalWritePlugin();
        var (context, _) = Context(plugin, new NiDaqDigitalWriteSetting { TaskName = "\"T\"", Values = buffered ? "new uint[] {1u, 2u}" : "1u" }, owner);
        var backend = new FakeBackend();
        try
        {
            Assert.Equal(buffered ? TestStatus.Completed : TestStatus.Error, (await new DaqExpansionExecutor(plugin, backend).ExecuteAsync(context)).StepResult.Status);
            Assert.False(owner.Started); Assert.Equal(buffered ? 1 : 0, backend.Writes);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Fact]
    public async Task Counter_output_config_does_not_start_or_write()
    {
        var plugin = new NiDaqCounterOutputConfigPlugin(); var (context, _) = ContextProxy.Create(plugin, new NiDaqCounterOutputConfigSetting { TaskName = "\"T\"", CounterChannel = "\"Fake/ctr0\"" });
        var backend = new FakeBackend(); var owner = new DaqTaskResource(() => { }, () => { });
        try
        {
            Assert.Equal(TestStatus.Completed, (await new DaqExpansionExecutor(plugin, backend, _ => owner).ExecuteAsync(context)).StepResult.Status);
            Assert.Equal(DaqTaskKind.CounterOutput, owner.Kind); Assert.False(owner.Started); Assert.Equal(0, backend.Writes); Assert.Equal(0, backend.Reads);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Fact]
    public async Task Config_cancel_retains_owner_until_real_exit_and_same_name_rerun_waits()
    {
        var plugin = new NiDaqDiConfigPlugin(); var (context, _) = ContextProxy.Create(plugin, new NiDaqDiConfigSetting { TaskName = "\"T\"", Channels = "\"Fake" + Guid.NewGuid().ToString("N") + "/port0\"", TimeoutMs = 3000 });
        await ExpressionEvaluatorFactory.Default.EvaluateAsync<string>("\"T\"", context);
        using var exit = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0; var owners = new List<DaqTaskResource>();
        var backend = new FakeBackend { OnConfigure = _ => { if (Interlocked.Increment(ref calls) == 1) { entered.TrySetResult(); exit.Wait(); } } };
        var executor = new DaqExpansionExecutor(plugin, backend, channels => { var o = new DaqTaskResource(() => { }, () => { }, channels); owners.Add(o); return o; });
        using var caller = new CancellationTokenSource(); var first = executor.ExecuteAsync(context, caller.Token); Task<ExecutionResult>? second = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); caller.Cancel(); Assert.Equal(TestStatus.Aborted, (await first.WaitAsync(TimeSpan.FromSeconds(2))).StepResult.Status);
            Assert.True(owners[0].IsClosing); Assert.False(owners[0].IsClosed);
            second = executor.ExecuteAsync(context); await Task.WhenAny(second, Task.Delay(50)); Assert.False(second.IsCompleted); Assert.Equal(1, calls);
            exit.Set(); Assert.Equal(TestStatus.Completed, (await second.WaitAsync(TimeSpan.FromSeconds(4))).StepResult.Status); Assert.Equal(2, calls);
        }
        finally { exit.Set(); await first; if (second != null) await second; foreach (var owner in owners) await owner.CloseAsync(3000, default); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pending_counter_read_never_publishes_after_cancel_or_replacement(bool replace)
    {
        var owner = await Owner(DaqTaskKind.CounterEdges); var replacement = await Owner(DaqTaskKind.CounterEdges);
        var plugin = new NiDaqCounterReadPlugin(); var (context, state) = Context(plugin, new NiDaqCounterReadSetting { TaskName = "\"T\"", ResultVariable = "Locals.r", TimeoutMs = 10000 }, owner);
        using var exit = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new FakeBackend { OnRead = () => { entered.TrySetResult(); exit.Wait(); } }; using var caller = new CancellationTokenSource();
        var operation = new DaqExpansionExecutor(plugin, backend).ExecuteAsync(context, caller.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (replace) { ((StoreProxy)state.Store!).Values["NiDaq.Task:T"] = replacement; exit.Set(); Assert.Equal(TestStatus.Error, (await operation).StepResult.Status); }
            else
            {
                caller.Cancel(); Assert.Equal(TestStatus.Aborted, (await operation.WaitAsync(TimeSpan.FromSeconds(2))).StepResult.Status);
                state.SetStep(new NiDaqTaskStopPlugin(), new NiDaqTaskStopSetting { TaskName = "\"T\"", TimeoutMs = 50 });
                Assert.Equal(TestStatus.Error, (await new NiDaqTaskStopExecutor().ExecuteAsync(context)).StepResult.Status);
                Assert.False(owner.IsClosed); Assert.Same(owner, ((StoreProxy)state.Store!).Values["NiDaq.Task:T"]);
                exit.Set(); await owner.CloseAsync(2000, default);
                Assert.Equal(TestStatus.Completed, (await new NiDaqTaskStopExecutor().ExecuteAsync(context)).StepResult.Status);
            }
            Assert.Empty(state.Published);
        }
        finally { exit.Set(); await operation; await owner.CloseAsync(3000, default); await replacement.CloseAsync(3000, default); }
    }

    private sealed class MustNeverEnumerate : IEnumerable
    { public bool Entered; public IEnumerator GetEnumerator() { Entered = true; throw new InvalidOperationException("禁止进入用户枚举器"); } }
    private sealed class FakeBackend : IDaqExpansionBackend
    {
        public int Writes, Reads; public uint[,]? LastWrite; public object CounterValue = 42u;
        public uint[,] DigitalValues = new uint[,] { { 1 } }; public Action? OnRead; public Action<DaqTaskResource>? OnConfigure;
        public void Configure(DaqTaskResource owner, DaqNamedSetting setting, string[] channels)
        {
            OnConfigure?.Invoke(owner); owner.EnsureOperationActive();
            owner.SetKind(setting switch { NiDaqCounterOutputConfigSetting => DaqTaskKind.CounterOutput, NiDaqCounterInputConfigSetting c => c.Mode == CounterInputMode.CountEdges ? DaqTaskKind.CounterEdges : DaqTaskKind.CounterFrequency, NiDaqDiConfigSetting => DaqTaskKind.DigitalInput, _ => DaqTaskKind.DigitalOutput });
            if (setting is DaqDigitalConfigSetting digital) owner.SetDigitalLayout(channels, channels.Select(_ => 8).ToArray(), digital.UseSampleClock);
        }
        public object ReadCounter(DaqTaskResource resource) { Reads++; OnRead?.Invoke(); return CounterValue; }
        public uint[,] ReadDigital(DaqTaskResource resource, int samples) { Reads++; OnRead?.Invoke(); return DigitalValues; }
        public void WriteDigital(DaqTaskResource resource, uint[,] values) { resource.EnsureOperationActive(); Writes++; LastWrite = values; }
    }
}
