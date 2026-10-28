using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace PluginRegression.Tests;

public sealed class ConfiguredFieldContractTests
{
    [Fact]
    public void Old_v1_maps_without_new_close_deadlines_keep_compatible_defaults()
    {
        IStepPlugin[] plugins = [new Ethernet.TcpClosePlugin(), new Ethernet.DoIP.DoipDisconnectPlugin(), new Http.HttpClientClosePlugin()];
        foreach (var plugin in plugins)
        {
            var restored = plugin.CreateSerializer().Deserialize([0x80], 1);
            Assert.Equal(5000, restored.GetType().GetProperty("TimeoutMs")!.GetValue(restored));
        }
    }

    [Fact]
    public async Task New_close_timeout_fields_are_documented_and_validated_before_execution()
    {
        (IStepPlugin Plugin, IStepEditorPlugin Editor)[] pairs = [
            (new Ethernet.TcpClosePlugin(), new Ethernet.UI.Editors.TcpCloseEditorPlugin()),
            (new Ethernet.DoIP.DoipDisconnectPlugin(), new Ethernet.UI.Editors.DoipDisconnectEditorPlugin()),
            (new Http.HttpClientClosePlugin(), new Http.UI.HttpClientCloseEditorPlugin())];
        foreach (var (plugin, editor) in pairs)
        {
            var setting = plugin.CreateSerializer().CreateDefault();
            setting.GetType().GetProperty("TimeoutMs")!.SetValue(setting, 0);
            Assert.Contains("TimeoutMs", plugin.Description);
            var (context, state) = TestContext.Create(plugin, setting);
            var errors = await editor.ValidateWithContextAsync(new StepEditorValidationContext
            {
                Setting = state.Current.Step.StepSetting.Setting, Evaluator = ExpressionEvaluatorFactory.Default,
                ExecutionContext = context, SequenceFile = new SequenceFile(), Block = [], CurrentStep = state.Current.Step
            });
            Assert.Contains(errors, error => error.Message.Contains("超时"));
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(xTestPlatform.Core.Models.TestStatus.Error, result.StepResult.Status);
        }
    }
}
