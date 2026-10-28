using NiDaq.Helpers;
using NiDaq.Models;
using System.IO;
using AuditTestSupport;
using NiDaq.Executors;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class AnalogOutputTests
{
    private static AoOutputPlan Plan(AoStopPolicy stop = AoStopPolicy.WriteFinalValues) =>
        AoOutputPlan.Create(new NiDaqAoConfigSetting
        {
            Channels = [new() { PhysicalChannel = "Dev1/ao0", ColumnName = "voltage", MinValue = -5, MaxValue = 5 }],
            StopPolicy = stop, CleanupTimeoutMs = 1000
        }, [1.0], stop == AoStopPolicy.WriteFinalValues ? [0.5] : null);

    [Fact]
    public async Task ConfigDoesNotOutputStartInitializesAndStopUsesExplicitFinalValue()
    {
        var calls = new List<string>();
        var owner = new DaqTaskResource(() => { }, () => calls.Add("release"));
        await owner.ConfigureAsync(() => owner.ConfigureAnalogOutput(Plan(),
            values => calls.Add("write:" + values[0]), () => calls.Add("start"), () => calls.Add("stop")), default);
        Assert.Empty(calls);
        await owner.StartAsync(default);
        await owner.WriteAnalogAsync([2.0], 1000, default);
        await owner.CloseAsync(1000, default);
        Assert.Equal(["start", "write:1", "write:2", "write:0.5", "stop", "release"], calls);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(6.0)]
    public void InvalidWholeVectorRejectedBeforeAnyWriter(double value)
        => Assert.ThrowsAny<ArgumentException>(() => Plan().ValidateValues([value]));

    [Fact]
    public void StopPolicyMustBeExplicitAndPlanOwnsImmutableValues()
    {
        Assert.Throws<ArgumentException>(() => Plan(AoStopPolicy.Unspecified));
        var plan = Plan();
        var first = plan.InitialValues;
        first[0] = 4;
        Assert.Equal(1, plan.InitialValues[0]);
    }

    [Fact]
    public async Task WriteWithoutStartAndPrecancelledWriteNeverSubmit()
    {
        int writes = 0;
        var owner = new DaqTaskResource(() => { }, () => { });
        await owner.ConfigureAsync(() => owner.ConfigureAnalogOutput(Plan(AoStopPolicy.DeviceDefined),
            _ => writes++, () => { }, () => { }), default);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.WriteAnalogAsync([2.0], 1000, default));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.WriteAnalogAsync([2.0], 1000, cancelled.Token));
            Assert.Equal(0, writes);
        }
        finally { await owner.CloseAsync(1000, default); }
    }

    [Fact]
    public async Task SuccessfulFinalWriteIsNotRepeatedWhenReleaseMustBeRetried()
    {
        int writes = 0, releases = 0;
        var owner = new DaqTaskResource(() => { }, () =>
        {
            if (Interlocked.Increment(ref releases) == 1) throw new InvalidOperationException("dispose failed");
        });
        await owner.ConfigureAsync(() => owner.ConfigureAnalogOutput(Plan(), _ => writes++, () => { }, () => { }), default);
        await owner.StartAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CloseAsync(1000, default));
        await owner.CloseAsync(1000, default);
        Assert.Equal(2, writes);
        Assert.True(owner.IsClosed);
    }

    [Fact]
    public async Task PartialStartDoesNotSkipConfiguredOutputCleanup()
    {
        int writes = 0, stops = 0, releases = 0, aborts = 0;
        var owner = new DaqTaskResource(() => aborts++, () => releases++);
        await owner.ConfigureAsync(() => owner.ConfigureAnalogOutput(Plan(), _ =>
        {
            if (++writes == 1) throw new IOException("initial write failed after native Start");
        }, () => { }, () => stops++), default);
        await Assert.ThrowsAsync<IOException>(() => owner.StartAsync(default));
        Assert.False(owner.Started);
        await owner.CloseAsync(1000, default);
        Assert.Equal(2, writes);
        Assert.Equal(1, stops);
        Assert.Equal(1, releases);
        Assert.Equal(0, aborts);
    }

    [Fact]
    public async Task PendingWriterMustReallyExitBeforeFinalValueStopAndRelease()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var nativeExit = new ManualResetEventSlim();
        var values = new List<double>();
        bool stopped = false, released = false;
        var owner = new DaqTaskResource(() => throw new Exception("AO must not Abort concurrently"), () => released = true);
        await owner.ConfigureAsync(() => owner.ConfigureAnalogOutput(Plan(), v =>
        {
            if (v[0] == 2) { entered.TrySetResult(); nativeExit.Wait(); }
            lock (values) values.Add(v[0]);
        }, () => { }, () => stopped = true), default);
        try
        {
            await owner.StartAsync(default);
            double[] input = [2];
            var write = owner.WriteAnalogAsync(input, 1000, default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            input[0] = 4;
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30, default));
            Assert.False(stopped || released);
            Assert.False(owner.IsClosed);
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.WriteAnalogAsync([3], 1000, default));
            nativeExit.Set();
            await Assert.ThrowsAsync<InvalidOperationException>(() => write);
            await owner.CloseAsync(1000, default);
            Assert.Equal([1.0, 2.0, 0.5], values);
            Assert.True(stopped && released && owner.IsClosed);
        }
        finally { nativeExit.Set(); if (!owner.IsClosed) await owner.CloseAsync(2000, default); }
    }

    [Fact]
    public async Task ExpiredCleanupNeverStartsNewOutputAfterDelayedWriterExit()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var exit = new ManualResetEventSlim();
        var plan = AoOutputPlan.Create(new NiDaqAoConfigSetting
        {
            Channels = [new() { PhysicalChannel = "Dev1/ao0", MinValue = -5, MaxValue = 5 }],
            StopPolicy = AoStopPolicy.WriteFinalValues, CleanupTimeoutMs = 40
        }, [1], [0.5]);
        int writes = 0;
        var owner = new DaqTaskResource(() => { }, () => { });
        await owner.ConfigureAsync(() => owner.ConfigureAnalogOutput(plan, v =>
        {
            Interlocked.Increment(ref writes);
            if (v[0] == 2) { entered.TrySetResult(); exit.Wait(); }
        }, () => { }, () => { }), default);
        await owner.StartAsync(default);
        var write = owner.WriteAnalogAsync([2], 1000, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var closing = owner.CloseAsync(500, default);
        await Task.Delay(80);
        exit.Set();
        await Assert.ThrowsAsync<InvalidOperationException>(() => write);
        await Assert.ThrowsAsync<TimeoutException>(() => closing);
        Assert.Equal(2, writes); // 过期的后台清理不能再写终值。
        Assert.False(owner.IsClosed);
        await owner.CloseAsync(1000, default); // 显式重试为尚未提交的清理建立新期限。
        Assert.Equal(3, writes);
        Assert.True(owner.IsClosed);
    }

    [Fact]
    public async Task UnknownFinalValueStillAttemptsStopButIsNeverBlindlyRepeated()
    {
        int writes = 0, stops = 0, releases = 0;
        var owner = new DaqTaskResource(() => { }, () => releases++);
        await owner.ConfigureAsync(() => owner.ConfigureAnalogOutput(Plan(), v =>
        {
            writes++;
            if (v[0] == 0.5) throw new IOException("driver write outcome unknown");
        }, () => { }, () => stops++), default);
        await owner.StartAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CloseAsync(1000, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CloseAsync(1000, default));
        Assert.Equal(2, writes);
        Assert.Equal(1, stops);
        Assert.Equal(0, releases);
        Assert.False(owner.IsClosed);
    }

    [Fact]
    public void ConversionAcceptsScalarAndNumericArrayButRejectsTextAndOversizedCollections()
    {
        Assert.Equal([2.0], AoOutputPlan.ConvertValues(2));
        Assert.Equal([1.0, 2.0], AoOutputPlan.ConvertValues(new List<double> { 1, 2 }));
        Assert.Throws<ArgumentException>(() => AoOutputPlan.ConvertValues("1.0"));
        Assert.Throws<ArgumentException>(() => AoOutputPlan.ConvertValues(true));
        Assert.Throws<ArgumentException>(() => AoOutputPlan.ConvertValues(Enumerable.Repeat(1, 257)));
    }

    [Fact]
    public void ConfigRejectsAmbiguousPhysicalChannelsAndRetiresTheCorrectEngine()
    {
        var setting = new NiDaqAoConfigSetting { Channels = [new() { PhysicalChannel = "Dev1/ao0:1" }], StopPolicy = AoStopPolicy.DeviceDefined };
        Assert.Throws<ArgumentException>(() => AoOutputPlan.Create(setting, [1]));
        setting.Channels[0].PhysicalChannel = "Dev1/ao0";
        setting.Channels.Add(new() { PhysicalChannel = "/dev1/AO0" });
        Assert.Throws<ArgumentException>(() => AoOutputPlan.Create(setting, [1, 2]));
        var keys = DaqPhysicalResources.Keys(["Dev1/ao0"]);
        Assert.Contains("DAQ:AoEngine:DEV1", keys);
        Assert.DoesNotContain("DAQ:AiEngine:DEV1", keys);
    }

    [Fact]
    public async Task CancelledConfigurationReturnsPromptlyButRetainsRealCleanupOwnership()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var exit = new ManualResetEventSlim();
        using var caller = new CancellationTokenSource();
        bool released = false;
        var owner = new DaqTaskResource(() => { }, () => released = true);
        var context = ContextProxy.Create();
        var configuring = DaqTaskFactory.CreateAndRegisterAsync(context, "cancelledAO", _ => { entered.TrySetResult(); exit.Wait(); }, caller.Token, owner, timeoutMs: 1500);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        try
        {
            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => configuring.WaitAsync(TimeSpan.FromMilliseconds(300)));
            Assert.Same(owner, NiDaqTaskRegistry.Get(context, "cancelledAO"));
            Assert.False(owner.IsClosed || released);
        }
        finally
        {
            exit.Set();
            try { await configuring; } catch { /* 仅测试 teardown：结果已在断言检查。 */ }
            await owner.CloseAsync(2000, default);
        }
        Assert.True(released && owner.IsClosed);
    }

    [Fact]
    public async Task FailedFinalPreparationIsNotAnUnknownSubmittedWriteAndMayBeRetried()
    {
        int writes = 0;
        bool failPreparation = false;
        var owner = new DaqTaskResource(() => { }, () => { });
        await owner.ConfigureAsync(() => owner.ConfigureAnalogOutput(Plan(), _ => writes++, () => { }, () => { }, () =>
        {
            if (failPreparation) { failPreparation = false; throw new TimeoutException("budget expired in timeout setter before voltage write"); }
        }), default);
        await owner.StartAsync(default);
        failPreparation = true;
        await Assert.ThrowsAnyAsync<Exception>(() => owner.CloseAsync(1000, default));
        Assert.Equal(1, writes); // 初值以外没有电压写提交。
        await owner.CloseAsync(1000, default);
        Assert.Equal(2, writes);
        Assert.True(owner.IsClosed);
    }

    [Fact]
    public async Task ActualStartWriteAndStopExecutorsUseTheSameAoOwnerAndReturnSdkReceipt()
    {
        var context = ContextProxy.Create();
        var values = new List<double>();
        var owner = new DaqTaskResource(() => { }, () => { });
        await owner.ConfigureAsync(() => owner.ConfigureAnalogOutput(Plan(), v => values.Add(v[0]), () => { }, () => { }), default);
        NiDaqTaskRegistry.Register(context, "AOTest", owner);
        void Step(IStepPlugin plugin, object setting)
        {
            var serializer = plugin.CreateSerializer();
            var step = new Step(); step.StepSetting.StepType = plugin.StepTypeId;
            step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion;
            context.CurrentStep = new StepExecutionInfo { Step = step };
        }
        try
        {
            Step(new NiDaqTaskStartPlugin(), new NiDaqTaskStartSetting { TaskName = "\"AOTest\"" });
            Assert.Equal(TestStatus.Completed, (await new NiDaqTaskStartExecutor().ExecuteAsync(context)).StepResult.Status);
            Step(new NiDaqAoWriteSinglePlugin(), new NiDaqAoWriteSingleSetting { TaskName = "\"AOTest\"", Values = "new double[] { 2.5 }" });
            var result = await new NiDaqAoWriteSingleExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.Contains("非实测电压", result.StepResult.Value);
            Step(new NiDaqTaskStopPlugin(), new NiDaqTaskStopSetting { TaskName = "\"AOTest\"" });
            Assert.Equal(TestStatus.Completed, (await new NiDaqTaskStopExecutor().ExecuteAsync(context)).StepResult.Status);
            Assert.Equal([1.0, 2.5, 0.5], values);
            Assert.True(owner.IsClosed);
        }
        finally { if (!owner.IsClosed) await owner.CloseAsync(2000, default); }
    }

    [Fact]
    public async Task InvalidAoConfigurationReportsErrorBeforeAnyDriverTaskCreation()
    {
        var context = ContextProxy.Create();
        var serializer = new NiDaqAoConfigPlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        context.CurrentStep = new StepExecutionInfo { Step = step };
        var result = await new NiDaqAoConfigExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("停止策略", result.StepResult.Error!.Message);
        Assert.Throws<InvalidOperationException>(() => NiDaqTaskRegistry.Get(context, "AoTask1"));
    }
}
