using System.Windows;
using StepEditor.Abstractions;
using UdpCommunication.StepPlugin.UI.Views;
using xTestPlatform.Core.SequenceModels;
using UdpCommunication.StepPlugin.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace UdpCommunication.StepPlugin.UI;

public sealed class UdpSendAndReceiveEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Network.UDP_SendAndReceive";
    public string IconPath => string.Empty;
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile) { var plugin = new UdpSendAndReceivePlugin(); var view = new UdpEditorView(plugin.CreateSerializer(), plugin.GenerateDescription, true); view.RefreshFromStep(step); return view; }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken cancellationToken = default) { var serializer = new UdpSendAndReceivePlugin().CreateSerializer(); return Task.FromResult(UdpEditorValidation.Validate(UdpEditorValidation.Deserialize(setting, serializer, true), true, context)); }
}
