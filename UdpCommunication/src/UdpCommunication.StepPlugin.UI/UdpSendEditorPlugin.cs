using System.Windows;
using StepEditor.Abstractions;
using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.UI.Views;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace UdpCommunication.StepPlugin.UI;

public sealed class UdpSendEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Network.UDP_Send";
    public string IconPath => string.Empty;
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile) { var view = new UdpEditorView(new UdpSendPlugin().CreateSerializer(), false); view.RefreshFromStep(step); return view; }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken cancellationToken = default) => Task.FromResult(UdpEditorValidation.Validate((UdpSendSetting)new UdpSendPlugin().CreateSerializer().Deserialize(setting, 1), false, context));
}
