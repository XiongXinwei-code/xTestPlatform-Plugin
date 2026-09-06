using System.Windows;
using VISA.Models;
using VISA.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI;

public sealed class VisaWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaWrite";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaWriteEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new VisaWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
