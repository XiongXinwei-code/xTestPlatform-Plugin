using System.Windows;
using CAN.Models;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanWrite";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanWriteEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new CanWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
