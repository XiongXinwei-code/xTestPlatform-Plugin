using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqSyncReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.SyncRead";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqSyncReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new NiDaqSyncReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
