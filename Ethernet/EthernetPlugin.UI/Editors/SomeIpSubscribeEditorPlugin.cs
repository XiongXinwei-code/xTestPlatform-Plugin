using System.Windows;
using Ethernet.SomeIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class SomeIpSubscribeEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "SomeIp.Subscribe";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SomeIpSubscribeEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }
}
