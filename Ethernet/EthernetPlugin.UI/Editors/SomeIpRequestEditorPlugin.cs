using System.Windows;
using Ethernet.SomeIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class SomeIpRequestEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "SomeIp.Request";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SomeIpRequestEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }
}
