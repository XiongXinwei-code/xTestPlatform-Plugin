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
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile) { var view = new UdpEditorView(new UdpSendAndReceivePlugin().CreateSerializer(), true); view.RefreshFromStep(step); return view; }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken cancellationToken = default) => Task.FromResult(UdpEditorValidation.Validate((UdpSendAndReceiveSetting)new UdpSendAndReceivePlugin().CreateSerializer().Deserialize(setting, 1), true, context));
}
