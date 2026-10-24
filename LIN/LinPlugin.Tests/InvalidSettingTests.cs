using System.Reflection;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace LIN.Tests;

public sealed class InvalidSettingTests
{
    [Fact]
    public async Task LifecycleEditorsRejectNonpositiveConfiguredTimeout()
    {
        foreach (bool open in new[] { true, false })
        {
            IStepPlugin plugin = open ? new LinOpenPlugin() : new LinClosePlugin();
            object setting = open ? new LIN.Models.LinOpenSetting { TimeoutMs = 0 } : new LIN.Models.LinCloseSetting { TimeoutMs = 0 };
            StepEditor.Abstractions.IStepEditorPlugin editor = open ? new LIN.UI.Editors.LinOpenEditorPlugin() : new LIN.UI.Editors.LinCloseEditorPlugin();
            var context = DispatchProxy.Create<IExecutionContext, LinContext>(); var state = (LinContext)(object)context;
            state.Plugin = plugin; state.Setting = setting;
            var step = context.CurrentStep!.Step;
            var validation = new StepEditor.Abstractions.StepEditorValidationContext { Setting = step.StepSetting.Setting, CurrentStep = step,
                Block = [step], SequenceFile = new xTestPlatform.Core.SequenceModels.SequenceFile(), ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default };
            Assert.Contains(await editor.ValidateWithContextAsync(validation), e => e.Code == "LIN_TIMEOUT");
        }
    }

    [Fact]
    public async Task EveryExecutorConvertsCorruptSettingsToErrorWithoutDeviceAccess()
    {
        foreach (var type in typeof(LinOpenPlugin).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(IStepPlugin).IsAssignableFrom(t)))
        {
            var plugin = (IStepPlugin)Activator.CreateInstance(type)!;
            var context = DispatchProxy.Create<IExecutionContext, LinContext>(); var state = (LinContext)(object)context;
            state.Plugin = plugin; state.PayloadOverride = [0xC1];
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.DoesNotContain(state.Items, item => !item.Key.StartsWith("PluginCommon.ExecutionEpoch:", StringComparison.Ordinal));
        }
    }
}
