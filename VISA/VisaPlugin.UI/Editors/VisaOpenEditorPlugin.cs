using System.Windows;
using VISA.Helpers;
using VISA.Models;
using VISA.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI;

public sealed class VisaOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaOpen";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaOpenEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new VisaOpenPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
