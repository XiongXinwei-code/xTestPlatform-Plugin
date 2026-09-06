using System.Windows;
using VISA.Models;
using VISA.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI;

public sealed class VisaWaitOpcEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaWaitOpc";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaWaitOpcEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new VisaWaitOpcPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
