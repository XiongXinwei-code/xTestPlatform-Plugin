using Ethernet;
using Ethernet.SomeIP;
using Ethernet.UI.Editors;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace PluginRegression.Tests;

public sealed class EthernetSendDeadlineContractTests
{
    public static IEnumerable<object[]> SendPlugins()
    {
        yield return [new UdpSendPlugin(), new UdpSendEditorPlugin()];
        yield return [new SomeIpFireAndForgetPlugin(), new SomeIpFireAndForgetEditorPlugin()];
    }

    [Theory]
    [MemberData(nameof(SendPlugins))]
    public void Old_v1_maps_receive_a_finite_compatible_send_deadline(IStepPlugin plugin, IStepEditorPlugin _)
    {
        var restored = plugin.CreateSerializer().Deserialize([0x80], 1);
        var property = restored.GetType().GetProperty("SendTimeoutMs");
        Assert.NotNull(property);
        Assert.Equal(3000, property.GetValue(restored));
        Assert.Contains("SendTimeoutMs", plugin.Description);
    }

    [Theory]
    [MemberData(nameof(SendPlugins))]
    public async Task New_send_deadline_is_validated_in_the_editor_and_executor(IStepPlugin plugin, IStepEditorPlugin editor)
    {
        var setting = plugin.CreateSerializer().CreateDefault();
        var property = setting.GetType().GetProperty("SendTimeoutMs");
        Assert.NotNull(property);
        property.SetValue(setting, 0);
        var (context, state) = TestContext.Create(plugin, setting);
        var errors = await editor.ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = state.Current.Step.StepSetting.Setting,
            Evaluator = ExpressionEvaluatorFactory.Default, ExecutionContext = context,
            SequenceFile = new SequenceFile(), Block = [], CurrentStep = state.Current.Step
        });
        Assert.Contains(errors, error => error.Message.Contains("超时"));
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("超时", result.StepResult.Error!.Message);
    }
}
