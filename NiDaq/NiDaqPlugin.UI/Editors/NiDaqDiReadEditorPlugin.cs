using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqDiReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.DiRead";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqDiReadEditorView();
        view.ViewModel.AttachSerializer(new NiDaqDiReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
