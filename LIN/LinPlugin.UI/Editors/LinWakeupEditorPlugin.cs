using System.Windows;
using LIN.Models;
using LIN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.Editors;

public sealed class LinWakeupEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.LinWakeup";
    public string IconPath   => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new LinWakeupEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new LinWakeupPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
