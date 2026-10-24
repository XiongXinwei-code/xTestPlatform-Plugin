using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace PluginRegression.Tests;

public sealed class Round1OutputValidationTests
{
    public static IEnumerable<object[]> OutputSteps => new (Type Plugin, Type Editor)[]
    {
        (typeof(Ethernet.TcpReceivePlugin),typeof(Ethernet.UI.Editors.TcpReceiveEditorPlugin)),
        (typeof(Ethernet.UdpReceivePlugin),typeof(Ethernet.UI.Editors.UdpReceiveEditorPlugin)),
        (typeof(Ethernet.DoIP.DoipDiagRequestPlugin),typeof(Ethernet.UI.Editors.DoipDiagRequestEditorPlugin)),
        (typeof(Ethernet.DoIP.DoipVehicleDiscoveryPlugin),typeof(Ethernet.UI.Editors.DoipVehicleDiscoveryEditorPlugin)),
        (typeof(Ethernet.SomeIP.SomeIpRequestPlugin),typeof(Ethernet.UI.Editors.SomeIpRequestEditorPlugin)),
        (typeof(Ethernet.SomeIP.SomeIpSubscribePlugin),typeof(Ethernet.UI.Editors.SomeIpSubscribeEditorPlugin)),
        (typeof(Ethernet.SomeIP.SomeIpSdDiscoverPlugin),typeof(Ethernet.UI.Editors.SomeIpSdDiscoverEditorPlugin))
    }.SelectMany(pair => new[] { new object[] { pair.Plugin, pair.Editor, false }, new object[] { pair.Plugin, pair.Editor, true } });

    [Theory]
    [MemberData(nameof(OutputSteps))]
    public async Task Optional_result_paths_must_be_declared_strings_if_configured(Type pluginType, Type editorType, bool wrongType)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(pluginType)!;
        var editor = (IStepEditorPlugin)Activator.CreateInstance(editorType)!;
        var setting = plugin.CreateSerializer().CreateDefault();
        setting.GetType().GetProperty("ResultVariable")!.SetValue(setting, "Locals.output");
        var (ctx, state) = TestContext.Create(plugin, setting);
        if (wrongType) state.Variables["Locals.output"] = 42;
        var errors = await editor.ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = state.Current.Step.StepSetting.Setting, Evaluator = ExpressionEvaluatorFactory.Default,
            ExecutionContext = ctx, SequenceFile = new SequenceFile(), Block = [state.Current.Step], CurrentStep = state.Current.Step
        });
        Assert.Contains(errors, error => error.Severity.ToString() == "Error" && error.Message.Contains("Locals.output"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Network_io_missing_named_open_is_a_warning_not_an_unchecked_dependency(bool doip, bool sending)
    {
        IStepPlugin plugin = doip ? new Ethernet.DoIP.DoipDiagRequestPlugin() : sending ? new Ethernet.TcpSendPlugin() : new Ethernet.TcpReceivePlugin();
        IStepEditorPlugin editor = doip ? new Ethernet.UI.Editors.DoipDiagRequestEditorPlugin() : sending ? new Ethernet.UI.Editors.TcpSendEditorPlugin() : new Ethernet.UI.Editors.TcpReceiveEditorPlugin();
        var (ctx, state) = TestContext.Create(plugin, plugin.CreateSerializer().CreateDefault());
        var errors = await editor.ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = state.Current.Step.StepSetting.Setting, Evaluator = ExpressionEvaluatorFactory.Default,
            ExecutionContext = ctx, SequenceFile = new SequenceFile(), Block = [state.Current.Step], CurrentStep = state.Current.Step
        });
        Assert.Contains(errors, error => error.Code is "ETH_LC01" or "DOIP_LC01" && error.Severity.ToString() == "Warning");
    }
}
