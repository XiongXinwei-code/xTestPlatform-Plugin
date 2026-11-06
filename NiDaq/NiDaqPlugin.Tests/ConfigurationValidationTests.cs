using NiDaq.Models;
using PluginTestCommon;
using StepEditor.Abstractions;
using System.Text.Json;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class ConfigurationValidationTests
{
    [Theory]
    [InlineData("ai-rate", "$.SampleRate")]
    [InlineData("ai-range", "$.Channels[0].MinValue")]
    [InlineData("ai-mode", "$.SampleMode")]
    [InlineData("ai-terminal", "$.Channels[0].Terminal")]
    [InlineData("sync-range", "$.AiChannels[0].MaxValue")]
    [InlineData("encoder-distance", "$.DistancePerPulse")]
    [InlineData("encoder-unit", "$.Unit")]
    public void NormalSaveRejectsInvalidConfigurationAtTheAffectedField(string kind, string field)
    {
        var (plugin, setting) = InvalidConfiguration(kind);
        var error = Assert.Throws<JsonException>(() => plugin.CreateSerializer().Serialize(setting));
        Assert.Contains(field, error.Message);
    }

    [Theory]
    [InlineData("ai-rate", "DAQ_003", true)]
    [InlineData("ai-range", "DAQ_007", true)]
    [InlineData("ai-mode", "DAQ_SAMPLE_MODE", true)]
    [InlineData("ai-terminal", "DAQ_TERMINAL", true)]
    [InlineData("ai-trigger", "DAQ_TRIGGER_SOURCE", false)]
    [InlineData("sync-range", "DAQ_027", true)]
    [InlineData("sync-encoder", "DAQ_SYNC_PPR", false)]
    [InlineData("sync-trigger", "DAQ_TRIGGER_SOURCE", false)]
    [InlineData("encoder-distance", "DAQ_043", true)]
    [InlineData("encoder-unit", "DAQ_ENCODER_UNIT", true)]
    public async Task ActualEditorsRejectInvalidConfigurationBeforeAnyHardwareRuns(string kind, string expectedCode, bool damaged)
    {
        var (plugin, setting) = InvalidConfiguration(kind);
        var bytes = damaged ? DamagedSettings.Serialize(plugin, setting) : plugin.CreateSerializer().Serialize(setting);
        var context = AuditTestSupport.ContextProxy.Create();
        var editorType = typeof(NiDaq.UI.NiDaqAiConfigEditorPlugin).Assembly.GetTypes().Single(type =>
            !type.IsAbstract && typeof(IStepEditorPlugin).IsAssignableFrom(type) &&
            ((IStepEditorPlugin)Activator.CreateInstance(type)!).StepTypeId == plugin.StepTypeId);
        var errors = await ((IStepEditorPlugin)Activator.CreateInstance(editorType)!).ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = bytes, Evaluator = ExpressionEvaluatorFactory.Default,
            ExecutionContext = context, SequenceFile = new SequenceFile(), Block = [], CurrentStep = new Step()
        });
        Assert.Contains(errors, error => error.Severity.ToString() == "Error" && error.Code == expectedCode);
    }

    private static (IStepPlugin Plugin, object Setting) InvalidConfiguration(string kind)
    {
        if (kind.StartsWith("ai-"))
        {
            var value = new NiDaqAiConfigSetting { Channels = [new() { PhysicalChannel = "TestDev/ai0", ColumnName = "AI" }] };
            if (kind == "ai-rate") value.SampleRate = double.NaN;
            if (kind == "ai-range") value.Channels[0].MinValue = double.NaN;
            if (kind == "ai-mode") value.SampleMode = (AiSampleMode)99;
            if (kind == "ai-terminal") value.Channels[0].Terminal = (AiTerminalConfig)99;
            if (kind == "ai-trigger") value.UseTrigger = true;
            return (new NiDaqAiConfigPlugin(), value);
        }
        else if (kind.StartsWith("sync-"))
        {
            var value = new NiDaqSyncConfigSetting
            { AiChannels = [new() { PhysicalChannel = "TestDev/ai0", ColumnName = "AI" }], EncoderChannels = [new() { CounterChannel = "TestDev/ctr0", ColumnName = "CI" }] };
            if (kind == "sync-range") value.AiChannels[0].MaxValue = double.PositiveInfinity;
            if (kind == "sync-encoder") value.EncoderChannels[0].PulsesPerRevolution = 0;
            if (kind == "sync-trigger") value.UseTrigger = true;
            return (new NiDaqSyncConfigPlugin(), value);
        }
        else
        {
            var value = new NiDaqEncoderConfigSetting { CounterChannel = "\"TestDev/ctr0\"" };
            if (kind == "encoder-distance") value.DistancePerPulse = double.NaN;
            if (kind == "encoder-unit") value.Unit = (EncoderUnit)99;
            return (new NiDaqEncoderConfigPlugin(), value);
        }
    }
}
