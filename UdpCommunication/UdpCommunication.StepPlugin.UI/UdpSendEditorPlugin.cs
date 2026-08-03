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
    public string IconPath => "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile) { var plugin = new UdpSendPlugin(); var view = new UdpEditorView(plugin.CreateSerializer(), plugin.GenerateDescription, false); view.RefreshFromStep(step); return view; }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken cancellationToken = default) { var serializer = new UdpSendPlugin().CreateSerializer(); return Task.FromResult(UdpEditorValidation.ValidateSafely(setting, serializer, false, context)); }
}
