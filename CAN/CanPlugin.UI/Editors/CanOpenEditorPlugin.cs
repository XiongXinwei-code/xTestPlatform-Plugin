using System.Windows;
using CAN.Helpers;
using CAN.Models;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanOpen";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanOpenEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new CanOpenPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
