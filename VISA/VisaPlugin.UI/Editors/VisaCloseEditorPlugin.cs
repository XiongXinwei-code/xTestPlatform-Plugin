using System.Windows;
using VISA.Models;
using VISA.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI;

public sealed class VisaCloseEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaClose";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaCloseEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new VisaClosePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
