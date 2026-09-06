using System.Windows;
using CAN.Models;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanCyclicSendStopEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanCyclicSendStop";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanCyclicSendStopEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new CanCyclicSendStopPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
