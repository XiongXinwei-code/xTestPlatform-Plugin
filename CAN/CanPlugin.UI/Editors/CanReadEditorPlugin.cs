using System.Windows;
using CAN.Models;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanRead";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new CanReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
