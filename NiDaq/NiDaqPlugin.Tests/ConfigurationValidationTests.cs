using NiDaq.Models;
using StepEditor.Abstractions;
using System.Reflection;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class ConfigurationValidationTests
{
    [Theory]
    [InlineData("ai-rate")]
    [InlineData("ai-range")]
    [InlineData("ai-mode")]
    [InlineData("ai-terminal")]
    [InlineData("ai-trigger")]
    [InlineData("sync-range")]
    [InlineData("sync-encoder")]
    [InlineData("sync-trigger")]
    [InlineData("encoder-distance")]
    [InlineData("encoder-unit")]
    public async Task ActualEditorsRejectInvalidConfigurationBeforeAnyHardwareRuns(string kind)
    {
        IStepPlugin plugin; object setting;
        if (kind.StartsWith("ai-"))
        {
            plugin = new NiDaqAiConfigPlugin();
            var value = new NiDaqAiConfigSetting { Channels = [new() { PhysicalChannel = "TestDev/ai0", ColumnName = "AI" }] };
            if (kind == "ai-rate") value.SampleRate = double.NaN;
            if (kind == "ai-range") value.Channels[0].MinValue = double.NaN;
            if (kind == "ai-mode") value.SampleMode = (AiSampleMode)99;
            if (kind == "ai-terminal") value.Channels[0].Terminal = (AiTerminalConfig)99;
            if (kind == "ai-trigger") value.UseTrigger = true;
            setting = value;
        }
        else if (kind.StartsWith("sync-"))
        {
            plugin = new NiDaqSyncConfigPlugin();
            var value = new NiDaqSyncConfigSetting
            { AiChannels = [new() { PhysicalChannel = "TestDev/ai0", ColumnName = "AI" }], EncoderChannels = [new() { CounterChannel = "TestDev/ctr0", ColumnName = "CI" }] };
            if (kind == "sync-range") value.AiChannels[0].MaxValue = double.PositiveInfinity;
            if (kind == "sync-encoder") value.EncoderChannels[0].PulsesPerRevolution = 0;
            if (kind == "sync-trigger") value.UseTrigger = true;
            setting = value;
        }
        else
        {
            plugin = new NiDaqEncoderConfigPlugin();
            var value = new NiDaqEncoderConfigSetting { CounterChannel = "\"TestDev/ctr0\"" };
            if (kind == "encoder-distance") value.DistancePerPulse = double.NaN;
            if (kind == "encoder-unit") value.Unit = (EncoderUnit)99;
            setting = value;
        }
        var context = AuditTestSupport.ContextProxy.Create();
        var editorType = typeof(NiDaq.UI.NiDaqAiConfigEditorPlugin).Assembly.GetTypes().Single(type =>
            !type.IsAbstract && typeof(IStepEditorPlugin).IsAssignableFrom(type) &&
            ((IStepEditorPlugin)Activator.CreateInstance(type)!).StepTypeId == plugin.StepTypeId);
        var errors = await ((IStepEditorPlugin)Activator.CreateInstance(editorType)!).ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = plugin.CreateSerializer().Serialize(setting), Evaluator = ExpressionEvaluatorFactory.Default,
            ExecutionContext = context, SequenceFile = new SequenceFile(), Block = [], CurrentStep = new Step()
        });
        Assert.Contains(errors, error => error.Severity.ToString() == "Error");
    }
}
