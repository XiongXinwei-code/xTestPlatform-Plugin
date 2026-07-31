using System.Windows;
using StepEditor.Abstractions;
using UdpCommunication.StepPlugin.UI.Views;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.StepPlugin.UI;

public sealed class UdpSendAndReceiveEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Network.UDP_SendAndReceive";
    public string IconPath => string.Empty;
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile) => new UdpEditorView(step, new UdpSendAndReceivePlugin().CreateSerializer(), true);
}
