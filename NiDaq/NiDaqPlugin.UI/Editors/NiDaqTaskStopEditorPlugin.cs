using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqTaskStopEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.TaskStop";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqTaskStopEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new NiDaqTaskStopPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
