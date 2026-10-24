using System.Reflection;
using CAN.CANopen.Cyclic;
using CAN.UDS.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class EditorValidationContractTests
{
    private static StepEditorValidationContext Context(IStepPlugin plugin, object setting)
    {
        var serializer = plugin.CreateSerializer();
        var step = new Step(); step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion;
        return new() { Setting = step.StepSetting.Setting, CurrentStep = step, Block = [step], SequenceFile = new SequenceFile(),
            ExecutionContext = DispatchProxy.Create<IExecutionContext, CloseContext>(), Evaluator = ExpressionEvaluatorFactory.Default };
    }

    [Fact]
    public async Task AllUdsEditorsRejectInvalidResponseLimit()
    {
        foreach (string name in new[] { "UdsDiagSession", "UdsClearDtc", "UdsRawRequest", "UdsReadDataById", "UdsReadDtc", "UdsRoutineControl", "UdsSecurityAccess", "UdsWriteDataById", "CanFlash" })
        {
            string ns = name == "CanFlash" ? "CAN.Flash" : "CAN.UDS";
            var plugin = (IStepPlugin)Activator.CreateInstance(typeof(CanOpenPlugin).Assembly.GetType($"{ns}.{name}Plugin")!)!;
            var setting = (UdsCommonSetting)plugin.CreateSerializer().CreateDefault(); setting.MaxResponseBytes = 0;
            var editor = (IStepEditorPlugin)Activator.CreateInstance(typeof(CAN.UI.CanOpenEditorPlugin).Assembly.GetType($"CAN.UI.{name}EditorPlugin")!)!;
            Assert.Contains(await editor.ValidateWithContextAsync(Context(plugin, setting)), e => e.Code == "UDS_RESPONSE_LIMIT");
            setting.MaxResponseBytes = 1024; setting.TransactionTimeoutMs = 0;
            Assert.Contains(await editor.ValidateWithContextAsync(Context(plugin, setting)), e => e.Code == "UDS_TRANSACTION_TIMEOUT");
            Assert.Equal(60000, ((UdsCommonSetting)plugin.CreateSerializer().Deserialize([0x80], 1)).TransactionTimeoutMs);
        }
    }

    [Fact]
    public async Task FiniteTrajectoryDoesNotRequireUnusedStreamingTargets()
    {
        var plugin = new CycleStartPlugin(); var setting = new CyclicSetting { UseFiniteTrajectory = true };
        var errors = await new CAN.UI.CycleStartEditorPlugin().ValidateWithContextAsync(Context(plugin, setting));
        Assert.DoesNotContain(errors, e => e.Code == "CY_TARGETS");
        setting.TrajectoryDurationMs = 0; setting.StopTimeoutMs = 0; setting.FeedbackStartupGraceMs = -1;
        errors = await new CAN.UI.CycleStartEditorPlugin().ValidateWithContextAsync(Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "CY_GRACE");
        Assert.True(errors.Count(e => e.Code == "CY_RANGE") >= 2);
    }

    [Fact]
    public async Task ConfigurableSendAndStopEditorsRejectNonpositiveBudget()
    {
        foreach (var row in SendBudgetRemediationTests.Settings())
        {
            var plugin = (IStepPlugin)row[0]; var setting = plugin.CreateSerializer().CreateDefault();
            var property = (string)row[1]; setting.GetType().GetProperty(property)!.SetValue(setting, 0);
            var name = plugin.GetType().Name.Replace("Plugin", "EditorPlugin");
            var editor = (IStepEditorPlugin)Activator.CreateInstance(typeof(CAN.UI.CanOpenEditorPlugin).Assembly.GetType("CAN.UI." + name)!)!;
            Assert.Contains(await editor.ValidateWithContextAsync(Context(plugin, setting)), e => e.Code is "CO_SEND_TIMEOUT" or "CAN_STOP_TIMEOUT");
        }
    }

    [Fact]
    public async Task MonitoringSyncValidatesItsOwnTiming()
    {
        var plugin = new ServiceStartPlugin(); var setting = new CyclicSetting { EnableMonitoringSync = true, MonitoringPeriodMs = 0 };
        var errors = await new CAN.UI.ServiceStartEditorPlugin().ValidateWithContextAsync(Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "CY_RANGE");
    }
}
