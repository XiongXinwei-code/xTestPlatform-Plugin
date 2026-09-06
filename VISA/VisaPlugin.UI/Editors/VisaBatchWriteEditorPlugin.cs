using System.Windows;
using VISA.Models;
using VISA.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI;

public sealed class VisaBatchWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaBatchWrite";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaBatchWriteEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new VisaBatchWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
