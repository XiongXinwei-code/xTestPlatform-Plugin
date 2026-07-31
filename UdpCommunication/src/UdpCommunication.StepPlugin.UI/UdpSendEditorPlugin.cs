using System.Windows;
using StepEditor.Abstractions;
using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.UI.Views;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.StepPlugin.UI;

public sealed class UdpSendEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Network.UDP_Send";
    public string IconPath => string.Empty;
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile) => new UdpEditorView(step, new UdpSendPlugin().CreateSerializer(), false);
}
