using AuditTestSupport;
using NiDaq.Executors;
using NiDaq.Helpers;
using NiDaq.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class DriverBoundaryTests
{
    [Fact]
    public void CancelledContinuationCannotPublishAnOldScalar()
    {
        var context = ContextProxy.Create();
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            DaqResultPublisher.Publish(context, "Locals.value", 2.0, caller.Token));
    }

    [Fact]
    public async Task ClosedOwnerCannotPublishAnOldScalar()
    {
        var owner = new DaqTaskResource(() => { }, () => { });
        await owner.ConfigureAsync(() => { }, default);
        await owner.CloseAsync(1000, default);
        Assert.Throws<InvalidOperationException>(() =>
            DaqResultPublisher.Publish(ContextProxy.Create(), "Locals.value", 2.0, default, owner));
    }

    [Fact]
    public async Task TaskStopReleasesExistingOwnerWithoutNativeDeviceEnumeration()
    {
        var context = ContextProxy.Create();
        bool released = false;
        var owner = new DaqTaskResource(() => { }, () => released = true);
        await owner.ConfigureAsync(() => { }, default);
        NiDaqTaskRegistry.Register(context, "NoNative", owner);
        var plugin = new NiDaqTaskStopPlugin();
        var serializer = plugin.CreateSerializer();
        var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(new NiDaqTaskStopSetting { TaskName = "\"NoNative\"", TimeoutMs = 500 });
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        context.CurrentStep = new StepExecutionInfo { Step = step };
        try
        {
            var result = await new NiDaqTaskStopExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.True(released);
        }
        finally { await owner.CloseAsync(2000, default); }
    }
}
