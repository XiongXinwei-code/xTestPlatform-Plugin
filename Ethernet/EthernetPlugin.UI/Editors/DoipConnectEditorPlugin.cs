using System.Windows;
using Ethernet.DoIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class DoipConnectEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "DoIP.Connect";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new DoipConnectEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }
}
